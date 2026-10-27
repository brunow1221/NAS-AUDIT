package cursor

import (
	"os"
	"path/filepath"
	"runtime"
	"testing"
)

func TestStorePersistsLastConfirmedCursor(t *testing.T) {
	path := filepath.Join(t.TempDir(), "checkpoint.json")
	store := NewStore(path)

	if err := store.Save(Checkpoint{Cursor: "s=confirmed", BootID: "boot-1"}); err != nil {
		t.Fatalf("save: %v", err)
	}
	checkpoint, err := store.Load()
	if err != nil {
		t.Fatalf("load: %v", err)
	}
	if checkpoint.Cursor != "s=confirmed" || checkpoint.BootID != "boot-1" {
		t.Fatalf("unexpected checkpoint: %#v", checkpoint)
	}
	if runtime.GOOS == "windows" {
		return
	}
	info, err := os.Stat(path)
	if err != nil {
		t.Fatalf("stat: %v", err)
	}
	if info.Mode().Perm()&0o077 != 0 {
		t.Fatalf("checkpoint permissions are too broad: %o", info.Mode().Perm())
	}
}

func TestStoreTreatsMissingFileAsEmptyCheckpoint(t *testing.T) {
	checkpoint, err := NewStore(filepath.Join(t.TempDir(), "missing.json")).Load()
	if err != nil || checkpoint.Cursor != "" {
		t.Fatalf("expected empty checkpoint, got %#v err=%v", checkpoint, err)
	}
}
