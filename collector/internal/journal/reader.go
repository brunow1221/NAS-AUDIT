package journal

import (
	"bufio"
	"context"
	"errors"
	"fmt"
	"io"
	"os/exec"
	"strings"
	"sync"

	"bcmetals.local/nas-audit/collector/internal/model"
)

type Reader struct {
	allowedSources []string
	command        *exec.Cmd
	mutex          sync.Mutex
}

type cursorUnavailableError struct {
	err error
}

func (e cursorUnavailableError) Error() string { return e.err.Error() }
func (e cursorUnavailableError) Unwrap() error { return e.err }

func IsCursorUnavailable(err error) bool {
	var target cursorUnavailableError
	return errors.As(err, &target)
}

func NewReader(allowedSources []string) *Reader {
	return &Reader{allowedSources: allowedSources}
}

func (r *Reader) Run(ctx context.Context, afterCursor string, output chan<- model.Event) error {
	arguments := []string{"--output=json", "--follow", "--no-pager", "--all"}
	if afterCursor != "" {
		arguments = append(arguments, "--after-cursor="+afterCursor)
	} else {
		arguments = append(arguments, "--since=now")
	}
	command := exec.CommandContext(ctx, "journalctl", arguments...)
	stdout, err := command.StdoutPipe()
	if err != nil {
		return fmt.Errorf("open journal output: %w", err)
	}
	stderr, err := command.StderrPipe()
	if err != nil {
		return fmt.Errorf("open journal error output: %w", err)
	}
	r.mutex.Lock()
	r.command = command
	r.mutex.Unlock()
	if err := command.Start(); err != nil {
		return fmt.Errorf("start journalctl: %w", err)
	}
	errorOutput := make(chan []byte, 1)
	go func() {
		content, _ := io.ReadAll(io.LimitReader(stderr, 32<<10))
		errorOutput <- content
	}()
	scanner := bufio.NewScanner(stdout)
	scanner.Buffer(make([]byte, 64<<10), 1<<20)
	for scanner.Scan() {
		event, accepted, parseErr := ParseLine(scanner.Bytes(), r.allowedSources)
		if parseErr != nil {
			continue
		}
		if !accepted {
			continue
		}
		select {
		case output <- event:
		case <-ctx.Done():
			return ctx.Err()
		}
	}
	if err := scanner.Err(); err != nil {
		return fmt.Errorf("read journal stream: %w", err)
	}
	err = command.Wait()
	if ctx.Err() != nil {
		return ctx.Err()
	}
	if err != nil {
		return classifyCommandError(err, <-errorOutput, afterCursor != "")
	}
	return nil
}

func classifyCommandError(commandError error, stderr []byte, usedCursor bool) error {
	detail := strings.TrimSpace(string(stderr))
	wrapped := fmt.Errorf("journalctl stopped: %w: %s", commandError, detail)
	lower := strings.ToLower(detail)
	if usedCursor && strings.Contains(lower, "cursor") &&
		(strings.Contains(lower, "seek") || strings.Contains(lower, "invalid") || strings.Contains(lower, "not found")) {
		return cursorUnavailableError{err: wrapped}
	}
	return wrapped
}
