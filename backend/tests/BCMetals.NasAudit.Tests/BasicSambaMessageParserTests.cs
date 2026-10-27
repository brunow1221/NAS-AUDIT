using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Domain.Enums;
using Xunit;

namespace BCMetals.NasAudit.Tests;

public sealed class BasicSambaMessageParserTests
{
    private readonly BasicSambaMessageParser _parser = new();

    [Fact]
    public void CreateFileCreateIsNotConfusedWithOpen()
    {
        var parsed = Parse("ugreen_syslog|soporte|client.example.invalid|create_file|ok|0x17019f|file|create|/volume1/PRUEBA_AUDITORIA_SMB/prueba.txt");

        Assert.Equal(AuditAction.Creation, parsed.Action);
        Assert.Equal(InterpretationConfidence.Confirmed, parsed.Confidence);
        Assert.Equal("PRUEBA_AUDITORIA_SMB", parsed.ShareName);
    }

    [Fact]
    public void CreateFileOpenIsNotReportedAsCreationOrDownload()
    {
        var parsed = Parse("ugreen_syslog|soporte|client.example.invalid|create_file|ok|0x100080|file|open|/volume1/PRUEBA_AUDITORIA_SMB/prueba.txt");

        Assert.Equal(AuditAction.Open, parsed.Action);
        Assert.NotEqual(AuditAction.Creation, parsed.Action);
        Assert.NotEqual(AuditAction.ObservedRead, parsed.Action);
    }

    [Fact]
    public void RenamePreservesSourceAndDestination()
    {
        var parsed = Parse("ugreen_syslog|soporte|client.example.invalid|renameat|ok|/volume1/PRUEBA_AUDITORIA_SMB/original.txt|/volume1/PRUEBA_AUDITORIA_SMB/nuevo.txt");

        Assert.Equal(AuditAction.Rename, parsed.Action);
        Assert.EndsWith("original.txt", parsed.SourcePath, StringComparison.Ordinal);
        Assert.EndsWith("nuevo.txt", parsed.DestinationPath, StringComparison.Ordinal);
    }

    [Fact]
    public void MoveToAnotherFolderIsNotReportedAsSimpleRename()
    {
        var parsed = Parse("ugreen_syslog|soporte|client.example.invalid|renameat|ok|/volume1/BCMETALS/origen/documento.txt|/volume1/BCMETALS/destino/documento.txt");

        Assert.Equal(AuditAction.Move, parsed.Action);
        Assert.Equal(InterpretationConfidence.Confirmed, parsed.Confidence);
    }

    [Fact]
    public void ParserFindsStructuredMessageInsideSyslogEnvelope()
    {
        var parsed = Parse("Oct 27 01:10:00 BCMETALSNAS smbd[42]: ugreen_syslog|usuario|client.example.invalid|pwrite|ok|/volume1/BCMETALS/Informe final.txt");

        Assert.Equal(AuditAction.Write, parsed.Action);
        Assert.Equal("usuario", parsed.SmbUser);
        Assert.EndsWith("Informe final.txt", parsed.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void AlternateDataStreamIsMarkedAsAuxiliaryEvidence()
    {
        var parsed = Parse("ugreen_syslog|usuario|client.example.invalid|create_file|ok|0x1|file|open|/volume1/BCMETALS/informe.txt:Zone.Identifier");

        Assert.True(parsed.IsAuxiliaryStream);
        Assert.Equal(AuditAction.Open, parsed.Action);
    }

    [Fact]
    public void MalformedMessageIsRejectedWithoutThrowing()
    {
        var result = _parser.TryParse("ugreen_syslog|incompleto", out var parsed);

        Assert.False(result);
        Assert.Null(parsed);
    }

    private ParsedAuditEvent Parse(string message)
    {
        Assert.True(_parser.TryParse(message, out var parsed));
        return Assert.IsType<ParsedAuditEvent>(parsed);
    }
}

