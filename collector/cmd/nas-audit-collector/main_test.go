package main

import (
	"context"
	"net/http"
	"net/http/httptest"
	"sync/atomic"
	"testing"
	"time"

	"bcmetals.local/nas-audit/collector/internal/config"
	"bcmetals.local/nas-audit/collector/internal/cursor"
	"bcmetals.local/nas-audit/collector/internal/model"
	"bcmetals.local/nas-audit/collector/internal/sender"
)

func TestSendUntilConfirmedRetriesAndPersistsAcknowledgedCursor(t *testing.T) {
	var attempts atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(writer http.ResponseWriter, _ *http.Request) {
		if attempts.Add(1) == 1 {
			http.Error(writer, "temporary failure", http.StatusServiceUnavailable)
			return
		}
		writer.Header().Set("Content-Type", "application/json")
		_, _ = writer.Write([]byte(`{"data":{"batchId":"batch","accepted":1,"duplicates":0,"rejected":0,"confirmedCursor":"cursor-1","replay":false}}`))
	}))
	defer server.Close()

	configuration := config.Config{CollectorID: "collector-test"}
	store := cursor.NewStore(t.TempDir() + "/checkpoint.json")
	client := sender.NewClient(server.URL, configuration.CollectorID, "0123456789abcdef0123456789abcdef", time.Second)
	state := &runtimeState{status: "healthy"}
	events := []model.Event{{Cursor: "cursor-1", BootID: "boot-1", TimestampUTC: time.Now().UTC(), Message: "ugreen_syslog|test"}}
	ctx, cancel := context.WithTimeout(context.Background(), 4*time.Second)
	defer cancel()

	remaining := sendUntilConfirmed(ctx, configuration, store, client, events, state)

	if len(remaining) != 0 || attempts.Load() != 2 {
		t.Fatalf("expected confirmed retry, remaining=%d attempts=%d", len(remaining), attempts.Load())
	}
	checkpoint, err := store.Load()
	if err != nil || checkpoint.Cursor != "cursor-1" {
		t.Fatalf("expected durable cursor, checkpoint=%#v error=%v", checkpoint, err)
	}
}
