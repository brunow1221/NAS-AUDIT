package journal

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"strconv"
	"strings"
	"time"

	"bcmetals.local/nas-audit/collector/internal/model"
)

var auditMarker = []byte("ugreen_syslog|")

type rawEntry map[string]json.RawMessage

func ParseLine(line []byte, allowedSources []string) (model.Event, bool, error) {
	var raw rawEntry
	if err := json.Unmarshal(line, &raw); err != nil {
		return model.Event{}, false, fmt.Errorf("decode journal JSON: %w", err)
	}
	message := stringValue(raw["MESSAGE"])
	if !bytes.Contains([]byte(message), auditMarker) {
		return model.Event{}, false, nil
	}
	if !sourceAllowed(stringValue(raw["_COMM"]), stringValue(raw["SYSLOG_IDENTIFIER"]), allowedSources) {
		return model.Event{}, false, nil
	}
	cursor := stringValue(raw["__CURSOR"])
	if cursor == "" {
		return model.Event{}, false, errors.New("journal entry has no __CURSOR")
	}
	timestamp, err := journalTimestamp(raw["__REALTIME_TIMESTAMP"])
	if err != nil {
		return model.Event{}, false, err
	}
	metadata := make(map[string]json.RawMessage)
	for _, key := range []string{"SYSLOG_IDENTIFIER", "_COMM", "_PID", "_SYSTEMD_UNIT", "_HOSTNAME"} {
		if value, ok := raw[key]; ok {
			metadata[key] = value
		}
	}
	return model.Event{
		Cursor: cursor, BootID: stringValue(raw["_BOOT_ID"]), TimestampUTC: timestamp,
		Message: message, Metadata: metadata,
	}, true, nil
}

func sourceAllowed(comm string, identifier string, allowed []string) bool {
	for _, candidate := range allowed {
		candidate = strings.TrimSpace(candidate)
		if candidate != "" && (strings.EqualFold(comm, candidate) || strings.EqualFold(identifier, candidate)) {
			return true
		}
	}
	return false
}

func stringValue(raw json.RawMessage) string {
	if len(raw) == 0 {
		return ""
	}
	var value string
	if json.Unmarshal(raw, &value) == nil {
		return value
	}
	return strings.Trim(string(raw), `"`)
}

func journalTimestamp(raw json.RawMessage) (time.Time, error) {
	value := stringValue(raw)
	microseconds, err := strconv.ParseInt(value, 10, 64)
	if err != nil {
		return time.Time{}, fmt.Errorf("invalid __REALTIME_TIMESTAMP: %w", err)
	}
	return time.UnixMicro(microseconds).UTC(), nil
}
