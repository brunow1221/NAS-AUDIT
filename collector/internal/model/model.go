package model

import (
	"encoding/json"
	"time"
)

type Event struct {
	Cursor       string                     `json:"cursor"`
	BootID       string                     `json:"bootId,omitempty"`
	TimestampUTC time.Time                  `json:"timestampUtc"`
	Message      string                     `json:"message"`
	Metadata     map[string]json.RawMessage `json:"metadata,omitempty"`
}

type BatchRequest struct {
	CollectorID string  `json:"collectorId"`
	BatchID     string  `json:"batchId"`
	Events      []Event `json:"events"`
}

type BatchResult struct {
	BatchID         string `json:"batchId"`
	Accepted        int    `json:"accepted"`
	Duplicates      int    `json:"duplicates"`
	Rejected        int    `json:"rejected"`
	ConfirmedCursor string `json:"confirmedCursor,omitempty"`
	Replay          bool   `json:"replay"`
}

type HeartbeatRequest struct {
	CollectorID string         `json:"collectorId"`
	Status      string         `json:"status"`
	ObservedAt  time.Time      `json:"observedAtUtc"`
	LastEventAt *time.Time     `json:"lastEventUtc,omitempty"`
	QueueDepth  int            `json:"queueDepth"`
	Message     string         `json:"message,omitempty"`
	Metadata    map[string]any `json:"metadata,omitempty"`
}

type Envelope[T any] struct {
	Data T `json:"data"`
}
