package sender

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"bcmetals.local/nas-audit/collector/internal/model"
)

func TestSendBatchAuthenticatesAndDecodesConfirmation(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		if request.Header.Get("X-Collector-Token") != "secret-token" || request.Header.Get("X-Collector-Id") != "collector-one" {
			t.Fatalf("missing collector authentication headers")
		}
		writer.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(writer).Encode(model.Envelope[model.BatchResult]{Data: model.BatchResult{
			BatchID: "batch-one", Accepted: 1, ConfirmedCursor: "s=confirmed",
		}})
	}))
	defer server.Close()
	client := NewClient(server.URL, "collector-one", "secret-token", time.Second)

	result, err := client.SendBatch(context.Background(), model.BatchRequest{
		CollectorID: "collector-one", BatchID: "batch-one", Events: []model.Event{{Cursor: "s=confirmed"}},
	})

	if err != nil {
		t.Fatalf("send batch: %v", err)
	}
	if result.Accepted != 1 || result.ConfirmedCursor != "s=confirmed" {
		t.Fatalf("unexpected result: %#v", result)
	}
}

func TestSendBatchReturnsTemporaryErrorForServerFailure(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(writer http.ResponseWriter, _ *http.Request) {
		http.Error(writer, "temporarily unavailable", http.StatusServiceUnavailable)
	}))
	defer server.Close()
	client := NewClient(server.URL, "collector-one", "secret-token", time.Second)

	_, err := client.SendBatch(context.Background(), model.BatchRequest{CollectorID: "collector-one", BatchID: "batch-one"})

	if err == nil || !IsTemporary(err) {
		t.Fatalf("expected temporary error, got %v", err)
	}
}
