package config

import "testing"

func TestLoadRejectsNonPositiveDurations(t *testing.T) {
	t.Setenv("COLLECTOR_TOKEN", "0123456789abcdef0123456789abcdef")
	t.Setenv("COLLECTOR_FLUSH_INTERVAL", "0s")

	if _, err := Load(); err == nil {
		t.Fatal("expected non-positive duration to be rejected")
	}
}
