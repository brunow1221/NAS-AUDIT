package config

import (
	"errors"
	"fmt"
	"net/url"
	"os"
	"regexp"
	"strconv"
	"strings"
	"time"
)

var collectorIDPattern = regexp.MustCompile(`^[a-zA-Z0-9][a-zA-Z0-9._-]{2,127}$`)

type Config struct {
	CollectorID       string
	APIBASEURL        string
	Token             string
	CursorPath        string
	SourceIdentifiers []string
	BatchSize         int
	QueueCapacity     int
	FlushInterval     time.Duration
	RequestTimeout    time.Duration
	HeartbeatInterval time.Duration
	HealthAddress     string
}

func Load() (Config, error) {
	configuration := Config{
		CollectorID:       value("COLLECTOR_ID", "bcmetalsnas-primary"),
		APIBASEURL:        value("COLLECTOR_API_URL", "http://nas-audit-api:8080"),
		Token:             strings.TrimSpace(os.Getenv("COLLECTOR_TOKEN")),
		CursorPath:        value("COLLECTOR_CURSOR_PATH", "/var/lib/nas-audit-collector/checkpoint.json"),
		SourceIdentifiers: list(value("COLLECTOR_SOURCE_IDENTIFIERS", "smbd")),
		BatchSize:         integer("COLLECTOR_BATCH_SIZE", 50),
		QueueCapacity:     integer("COLLECTOR_QUEUE_CAPACITY", 500),
		FlushInterval:     duration("COLLECTOR_FLUSH_INTERVAL", 2*time.Second),
		RequestTimeout:    duration("COLLECTOR_REQUEST_TIMEOUT", 15*time.Second),
		HeartbeatInterval: duration("COLLECTOR_HEARTBEAT_INTERVAL", 20*time.Second),
		HealthAddress:     value("COLLECTOR_HEALTH_ADDRESS", ":8081"),
	}
	if len(configuration.Token) < 32 {
		return Config{}, errors.New("COLLECTOR_TOKEN must contain at least 32 characters")
	}
	if !collectorIDPattern.MatchString(configuration.CollectorID) {
		return Config{}, errors.New("COLLECTOR_ID has an invalid format")
	}
	parsedURL, err := url.Parse(configuration.APIBASEURL)
	if err != nil || parsedURL.Host == "" || (parsedURL.Scheme != "http" && parsedURL.Scheme != "https") {
		return Config{}, errors.New("COLLECTOR_API_URL must be an absolute HTTP(S) URL")
	}
	if configuration.BatchSize < 1 || configuration.BatchSize > 200 {
		return Config{}, errors.New("COLLECTOR_BATCH_SIZE must be between 1 and 200")
	}
	if configuration.QueueCapacity < configuration.BatchSize || configuration.QueueCapacity > 10000 {
		return Config{}, errors.New("COLLECTOR_QUEUE_CAPACITY must be between batch size and 10000")
	}
	if configuration.FlushInterval <= 0 || configuration.RequestTimeout <= 0 || configuration.HeartbeatInterval <= 0 {
		return Config{}, errors.New("collector durations must be positive")
	}
	if len(configuration.SourceIdentifiers) == 0 {
		return Config{}, errors.New("COLLECTOR_SOURCE_IDENTIFIERS must not be empty")
	}
	return configuration, nil
}

func value(name string, fallback string) string {
	if result := strings.TrimSpace(os.Getenv(name)); result != "" {
		return result
	}
	return fallback
}

func list(value string) []string {
	var result []string
	for _, part := range strings.Split(value, ",") {
		if candidate := strings.TrimSpace(part); candidate != "" {
			result = append(result, candidate)
		}
	}
	return result
}

func integer(name string, fallback int) int {
	value := strings.TrimSpace(os.Getenv(name))
	if value == "" {
		return fallback
	}
	parsed, err := strconv.Atoi(value)
	if err != nil {
		return -1
	}
	return parsed
}

func duration(name string, fallback time.Duration) time.Duration {
	value := strings.TrimSpace(os.Getenv(name))
	if value == "" {
		return fallback
	}
	parsed, err := time.ParseDuration(value)
	if err != nil {
		return -1
	}
	return parsed
}

func (c Config) String() string {
	return fmt.Sprintf("collector=%s api=%s queue=%d batch=%d sources=%s",
		c.CollectorID, c.APIBASEURL, c.QueueCapacity, c.BatchSize, strings.Join(c.SourceIdentifiers, ","))
}
