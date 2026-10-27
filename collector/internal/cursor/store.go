package cursor

import (
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"time"
)

type Checkpoint struct {
	Cursor    string    `json:"cursor"`
	BootID    string    `json:"bootId,omitempty"`
	UpdatedAt time.Time `json:"updatedAtUtc"`
}

type Store struct {
	path string
}

func NewStore(path string) *Store {
	return &Store{path: path}
}

func (s *Store) Load() (Checkpoint, error) {
	content, err := os.ReadFile(s.path)
	if errors.Is(err, os.ErrNotExist) {
		return Checkpoint{}, nil
	}
	if err != nil {
		return Checkpoint{}, fmt.Errorf("read checkpoint: %w", err)
	}
	var checkpoint Checkpoint
	if err := json.Unmarshal(content, &checkpoint); err != nil {
		return Checkpoint{}, fmt.Errorf("decode checkpoint: %w", err)
	}
	return checkpoint, nil
}

func (s *Store) Save(checkpoint Checkpoint) error {
	checkpoint.UpdatedAt = time.Now().UTC()
	content, err := json.Marshal(checkpoint)
	if err != nil {
		return fmt.Errorf("encode checkpoint: %w", err)
	}
	directory := filepath.Dir(s.path)
	if err := os.MkdirAll(directory, 0o700); err != nil {
		return fmt.Errorf("create checkpoint directory: %w", err)
	}
	temporary, err := os.CreateTemp(directory, ".checkpoint-*")
	if err != nil {
		return fmt.Errorf("create temporary checkpoint: %w", err)
	}
	temporaryPath := temporary.Name()
	defer os.Remove(temporaryPath)
	if err := temporary.Chmod(0o600); err != nil {
		temporary.Close()
		return fmt.Errorf("protect temporary checkpoint: %w", err)
	}
	if _, err := temporary.Write(content); err != nil {
		temporary.Close()
		return fmt.Errorf("write temporary checkpoint: %w", err)
	}
	if err := temporary.Sync(); err != nil {
		temporary.Close()
		return fmt.Errorf("sync temporary checkpoint: %w", err)
	}
	if err := temporary.Close(); err != nil {
		return fmt.Errorf("close temporary checkpoint: %w", err)
	}
	if err := os.Rename(temporaryPath, s.path); err != nil {
		return fmt.Errorf("replace checkpoint: %w", err)
	}
	return os.Chmod(s.path, 0o600)
}
