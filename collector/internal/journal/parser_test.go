package journal

import (
	"testing"
	"time"
)

func TestParseLineAcceptsOriginalSambaAuditEntry(t *testing.T) {
	line := []byte(`{"MESSAGE":"ugreen_syslog|soporte|client.example.invalid|pwrite|ok|/volume1/PRUEBA_AUDITORIA_SMB/test.txt","__CURSOR":"s=cursor-1","__REALTIME_TIMESTAMP":"1793102400000000","_BOOT_ID":"boot-1","_COMM":"smbd","SYSLOG_IDENTIFIER":"smbd"}`)

	event, ok, err := ParseLine(line, []string{"smbd"})

	if err != nil || !ok {
		t.Fatalf("expected accepted event, ok=%v err=%v", ok, err)
	}
	if event.Cursor != "s=cursor-1" || event.Message == "" {
		t.Fatalf("unexpected event: %#v", event)
	}
	if event.TimestampUTC.Location() != time.UTC {
		t.Fatalf("timestamp must be UTC: %v", event.TimestampUTC)
	}
}

func TestParseLineRejectsSecondaryReceiverRepresentation(t *testing.T) {
	line := []byte(`{"MESSAGE":"ugreen_syslog|soporte|client.example.invalid|pwrite|ok|/volume1/PRUEBA_AUDITORIA_SMB/test.txt","__CURSOR":"s=cursor-2","__REALTIME_TIMESTAMP":"1793102400000000","_COMM":"ugreen-log-receiver","SYSLOG_IDENTIFIER":"ugreen_syslog"}`)

	_, ok, err := ParseLine(line, []string{"smbd"})

	if err != nil || ok {
		t.Fatalf("expected filtered secondary entry, ok=%v err=%v", ok, err)
	}
}

func TestParseLineRejectsMissingCursor(t *testing.T) {
	line := []byte(`{"MESSAGE":"ugreen_syslog|soporte|client.example.invalid|pwrite|ok|/volume1/test.txt","__REALTIME_TIMESTAMP":"1793102400000000","_COMM":"smbd"}`)

	_, ok, err := ParseLine(line, []string{"smbd"})

	if err == nil || ok {
		t.Fatalf("expected invalid entry, ok=%v err=%v", ok, err)
	}
}
