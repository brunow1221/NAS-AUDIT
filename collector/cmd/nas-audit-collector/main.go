package main

import (
	"context"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	"bcmetals.local/nas-audit/collector/internal/config"
	"bcmetals.local/nas-audit/collector/internal/cursor"
	"bcmetals.local/nas-audit/collector/internal/journal"
	"bcmetals.local/nas-audit/collector/internal/model"
	"bcmetals.local/nas-audit/collector/internal/sender"
)

type runtimeState struct {
	mutex       sync.RWMutex
	status      string
	message     string
	lastEventAt *time.Time
	read        uint64
	confirmed   uint64
	retries     uint64
	queue       int
}

type stateSnapshot struct {
	Status      string     `json:"status"`
	Message     string     `json:"message,omitempty"`
	LastEventAt *time.Time `json:"lastEventUtc,omitempty"`
	Read        uint64     `json:"eventsRead"`
	Confirmed   uint64     `json:"eventsConfirmed"`
	Retries     uint64     `json:"retries"`
	Queue       int        `json:"queueDepth"`
}

func main() {
	if len(os.Args) == 2 && os.Args[1] == "--healthcheck" {
		healthcheck()
		return
	}
	configuration, err := config.Load()
	if err != nil {
		slog.Error("invalid collector configuration", "error", err)
		os.Exit(2)
	}
	slog.Info("starting BCMETALS NAS audit collector", "configuration", configuration.String())

	ctx, cancel := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer cancel()
	checkpointStore := cursor.NewStore(configuration.CursorPath)
	client := sender.NewClient(configuration.APIBASEURL, configuration.CollectorID, configuration.Token, configuration.RequestTimeout)
	events := make(chan model.Event, configuration.QueueCapacity)
	state := &runtimeState{status: "recovering", message: "Iniciando lector de journald"}

	go runJournal(ctx, configuration, checkpointStore, events, state)
	go runBatches(ctx, configuration, checkpointStore, client, events, state)
	go runHeartbeats(ctx, configuration, client, state)
	server := startHealthServer(configuration.HealthAddress, state)

	<-ctx.Done()
	shutdownCtx, shutdownCancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer shutdownCancel()
	_ = server.Shutdown(shutdownCtx)
	slog.Info("collector stopped")
}

func healthcheck() {
	client := &http.Client{Timeout: 2 * time.Second}
	response, err := client.Get("http://localhost:8081/healthz")
	if err != nil || response.StatusCode != http.StatusOK {
		os.Exit(1)
	}
	_ = response.Body.Close()
}

func runJournal(
	ctx context.Context,
	configuration config.Config,
	store *cursor.Store,
	events chan<- model.Event,
	state *runtimeState,
) {
	backoff := time.Second
	for ctx.Err() == nil {
		checkpoint, err := store.Load()
		if err != nil {
			state.update("error", err.Error())
			wait(ctx, backoff)
			backoff = nextBackoff(backoff)
			continue
		}
		state.update("healthy", "Fuente journald conectada")
		reader := journal.NewReader(configuration.SourceIdentifiers)
		err = reader.Run(ctx, checkpoint.Cursor, events)
		if ctx.Err() != nil {
			return
		}
		if checkpoint.Cursor != "" && journal.IsCursorUnavailable(err) {
			message := "Cursor journald ya no disponible; posible brecha registrada y recuperación desde el momento actual"
			state.update("recovering", message)
			slog.Error("journal cursor unavailable", "error", err, "possibleGap", true)
			if saveErr := store.Save(cursor.Checkpoint{}); saveErr != nil {
				state.update("error", saveErr.Error())
			}
			wait(ctx, time.Second)
			continue
		}
		state.update("source_unavailable", err.Error())
		slog.Error("journal reader stopped", "error", err, "retryIn", backoff)
		wait(ctx, backoff)
		state.update("recovering", "Reconectando con journald")
		backoff = nextBackoff(backoff)
	}
}

func runBatches(
	ctx context.Context,
	configuration config.Config,
	store *cursor.Store,
	client *sender.Client,
	events <-chan model.Event,
	state *runtimeState,
) {
	ticker := time.NewTicker(configuration.FlushInterval)
	defer ticker.Stop()
	batch := make([]model.Event, 0, configuration.BatchSize)
	for {
		select {
		case <-ctx.Done():
			return
		case event := <-events:
			batch = append(batch, event)
			state.eventRead(event.TimestampUTC, len(events)+len(batch))
			if len(batch) >= configuration.BatchSize {
				batch = sendUntilConfirmed(ctx, configuration, store, client, batch, state)
			}
		case <-ticker.C:
			if len(batch) > 0 {
				batch = sendUntilConfirmed(ctx, configuration, store, client, batch, state)
			}
		}
	}
}

func sendUntilConfirmed(
	ctx context.Context,
	configuration config.Config,
	store *cursor.Store,
	client *sender.Client,
	batch []model.Event,
	state *runtimeState,
) []model.Event {
	request := model.BatchRequest{CollectorID: configuration.CollectorID, BatchID: newUUID(), Events: batch}
	backoff := time.Second
	for ctx.Err() == nil {
		result, err := client.SendBatch(ctx, request)
		if err == nil {
			if result.ConfirmedCursor != "" {
				bootID := bootIDForCursor(batch, result.ConfirmedCursor)
				if saveErr := store.Save(cursor.Checkpoint{Cursor: result.ConfirmedCursor, BootID: bootID}); saveErr != nil {
					state.update("error", saveErr.Error())
					slog.Error("could not persist confirmed cursor", "error", saveErr)
					wait(ctx, backoff)
					backoff = nextBackoff(backoff)
					continue
				}
			}
			state.batchConfirmed(uint64(result.Accepted + result.Duplicates))
			return batch[:0]
		}
		state.retry(err.Error())
		slog.Warn("batch delivery failed", "error", err, "batchId", request.BatchID, "retryIn", backoff)
		wait(ctx, backoff)
		backoff = nextBackoff(backoff)
	}
	return batch
}

func runHeartbeats(ctx context.Context, configuration config.Config, client *sender.Client, state *runtimeState) {
	ticker := time.NewTicker(configuration.HeartbeatInterval)
	defer ticker.Stop()
	for {
		snapshot := state.snapshot()
		heartbeat := model.HeartbeatRequest{
			CollectorID: configuration.CollectorID, Status: snapshot.Status, ObservedAt: time.Now().UTC(),
			LastEventAt: snapshot.LastEventAt, QueueDepth: snapshot.Queue, Message: snapshot.Message,
			Metadata: map[string]any{"eventsRead": snapshot.Read, "eventsConfirmed": snapshot.Confirmed, "retries": snapshot.Retries},
		}
		heartbeatCtx, cancel := context.WithTimeout(ctx, configuration.RequestTimeout)
		err := client.SendHeartbeat(heartbeatCtx, heartbeat)
		cancel()
		if err != nil && !errors.Is(err, context.Canceled) {
			slog.Warn("heartbeat failed", "error", err)
		}
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
		}
	}
}

func startHealthServer(address string, state *runtimeState) *http.Server {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", func(writer http.ResponseWriter, _ *http.Request) {
		snapshot := state.snapshot()
		writer.Header().Set("Content-Type", "application/json")
		if snapshot.Status == "error" || snapshot.Status == "source_unavailable" {
			writer.WriteHeader(http.StatusServiceUnavailable)
		}
		_ = json.NewEncoder(writer).Encode(snapshot)
	})
	mux.HandleFunc("GET /metrics", func(writer http.ResponseWriter, _ *http.Request) {
		writer.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(writer).Encode(state.snapshot())
	})
	server := &http.Server{Addr: address, Handler: mux, ReadHeaderTimeout: 5 * time.Second}
	go func() {
		if err := server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			slog.Error("health server failed", "error", err)
		}
	}()
	return server
}

func (s *runtimeState) update(status string, message string) {
	s.mutex.Lock()
	defer s.mutex.Unlock()
	s.status, s.message = status, message
}

func (s *runtimeState) eventRead(timestamp time.Time, queue int) {
	s.mutex.Lock()
	defer s.mutex.Unlock()
	s.read++
	timestamp = timestamp.UTC()
	s.lastEventAt = &timestamp
	s.queue = queue
}

func (s *runtimeState) batchConfirmed(count uint64) {
	s.mutex.Lock()
	defer s.mutex.Unlock()
	s.confirmed += count
	s.queue = 0
	if s.status == "recovering" {
		s.status = "healthy"
	}
}

func (s *runtimeState) retry(message string) {
	s.mutex.Lock()
	defer s.mutex.Unlock()
	s.retries++
	s.status = "recovering"
	s.message = message
}

func (s *runtimeState) snapshot() stateSnapshot {
	s.mutex.RLock()
	defer s.mutex.RUnlock()
	return stateSnapshot{Status: s.status, Message: s.message, LastEventAt: s.lastEventAt, Read: s.read, Confirmed: s.confirmed, Retries: s.retries, Queue: s.queue}
}

func newUUID() string {
	value := make([]byte, 16)
	if _, err := rand.Read(value); err != nil {
		panic(fmt.Sprintf("generate batch UUID: %v", err))
	}
	value[6] = (value[6] & 0x0f) | 0x40
	value[8] = (value[8] & 0x3f) | 0x80
	hexValue := hex.EncodeToString(value)
	return fmt.Sprintf("%s-%s-%s-%s-%s", hexValue[0:8], hexValue[8:12], hexValue[12:16], hexValue[16:20], hexValue[20:32])
}

func bootIDForCursor(events []model.Event, confirmedCursor string) string {
	for _, event := range events {
		if event.Cursor == confirmedCursor {
			return event.BootID
		}
	}
	return ""
}

func nextBackoff(current time.Duration) time.Duration {
	if current >= 30*time.Second {
		return 30 * time.Second
	}
	return current * 2
}

func wait(ctx context.Context, duration time.Duration) {
	select {
	case <-ctx.Done():
	case <-time.After(duration):
	}
}
