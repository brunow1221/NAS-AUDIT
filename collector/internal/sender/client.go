package sender

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
	"time"

	"bcmetals.local/nas-audit/collector/internal/model"
)

type temporaryError struct {
	err error
}

func (e temporaryError) Error() string { return e.err.Error() }
func (e temporaryError) Unwrap() error { return e.err }

func IsTemporary(err error) bool {
	_, ok := err.(temporaryError)
	return ok
}

type Client struct {
	baseURL     string
	collectorID string
	token       string
	http        *http.Client
}

func NewClient(baseURL string, collectorID string, token string, timeout time.Duration) *Client {
	return &Client{
		baseURL: strings.TrimRight(baseURL, "/"), collectorID: collectorID, token: token,
		http: &http.Client{Timeout: timeout},
	}
}

func (c *Client) SendBatch(ctx context.Context, batch model.BatchRequest) (model.BatchResult, error) {
	var envelope model.Envelope[model.BatchResult]
	err := c.post(ctx, "/api/v1/ingestion/events", batch, &envelope)
	return envelope.Data, err
}

func (c *Client) SendHeartbeat(ctx context.Context, heartbeat model.HeartbeatRequest) error {
	var envelope model.Envelope[map[string]bool]
	return c.post(ctx, "/api/v1/ingestion/heartbeat", heartbeat, &envelope)
}

func (c *Client) post(ctx context.Context, path string, payload any, destination any) error {
	content, err := json.Marshal(payload)
	if err != nil {
		return fmt.Errorf("encode request: %w", err)
	}
	request, err := http.NewRequestWithContext(ctx, http.MethodPost, c.baseURL+path, bytes.NewReader(content))
	if err != nil {
		return fmt.Errorf("create request: %w", err)
	}
	request.Header.Set("Content-Type", "application/json")
	request.Header.Set("X-Collector-Id", c.collectorID)
	request.Header.Set("X-Collector-Token", c.token)
	response, err := c.http.Do(request)
	if err != nil {
		return temporaryError{err: fmt.Errorf("send request: %w", err)}
	}
	defer response.Body.Close()
	body, err := io.ReadAll(io.LimitReader(response.Body, 1<<20))
	if err != nil {
		return temporaryError{err: fmt.Errorf("read response: %w", err)}
	}
	if response.StatusCode < 200 || response.StatusCode >= 300 {
		failure := fmt.Errorf("API returned %d: %s", response.StatusCode, strings.TrimSpace(string(body)))
		if response.StatusCode == http.StatusTooManyRequests || response.StatusCode >= 500 {
			return temporaryError{err: failure}
		}
		return failure
	}
	if err := json.Unmarshal(body, destination); err != nil {
		return fmt.Errorf("decode response: %w", err)
	}
	return nil
}
