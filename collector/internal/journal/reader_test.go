package journal

import (
	"errors"
	"testing"
)

func TestCursorUnavailableIsClassifiedForRecovery(t *testing.T) {
	err := classifyCommandError(errors.New("exit status 1"), []byte("Failed to seek to cursor: Invalid argument"), true)

	if !IsCursorUnavailable(err) {
		t.Fatalf("expected cursor-unavailable error, got %v", err)
	}
}

func TestPermissionFailureIsNotClassifiedAsCursorRotation(t *testing.T) {
	err := classifyCommandError(errors.New("exit status 1"), []byte("Failed to open journal: Permission denied"), true)

	if IsCursorUnavailable(err) {
		t.Fatalf("permission failure must not reset cursor: %v", err)
	}
}
