using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeoEcosystem.Contracts.Inspection;

/// <summary>Tipos de mensagem do GADX (SPEC §5).</summary>
public enum InspectionPipeMessageType : ushort
{
    Hello = 1,
    HelloAck = 2,
    InspectionPush = 3,
    InspectionAck = 4,
    Error = 5,
    CorrectionPush = 6,
    CorrectionAck = 7,
}

public sealed record InspectionPipeFrame(
    ushort ProtocolVersion,
    InspectionPipeMessageType MessageType,
    int RequestId,
    byte[] Payload);

/// <summary>Frame do GADX: cabeçalho de 16 bytes little-endian + payload JSON (SPEC §3).</summary>
public static class InspectionPipeProtocol
{
    public const string DefaultPipeName = "GeoAutoEngine.Inspection";
    public const string Magic = "GADX";

    /// <summary>Versão no fio. A revisão 1.1 da especificação continua usando 1 (SPEC §1).</summary>
    public const ushort CurrentVersion = 1;

    public const int HeaderLength = 16;
    public const int MaxPayloadBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions ProtocolJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes(Magic);

    public static byte[] SerializePayload<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, ProtocolJsonOptions);

    public static T DeserializePayload<T>(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<T>(payload, ProtocolJsonOptions)
        ?? throw new InvalidDataException("Payload do protocolo vazio ou inválido.");

    public static Task WriteFrameAsync(
        Stream stream,
        InspectionPipeMessageType messageType,
        int requestId,
        byte[] payload,
        CancellationToken cancellationToken = default,
        ushort protocolVersion = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return WriteFrameAsync(
            stream,
            messageType,
            requestId,
            new ReadOnlyMemory<byte>(payload),
            cancellationToken,
            protocolVersion);
    }

    public static async Task WriteFrameAsync(
        Stream stream,
        InspectionPipeMessageType messageType,
        int requestId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default,
        ushort protocolVersion = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.Length > MaxPayloadBytes)
        {
            throw new InvalidDataException($"Payload excede o limite de {MaxPayloadBytes} bytes.");
        }

        var header = new byte[HeaderLength];
        MagicBytes.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4, 2), protocolVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6, 2), (ushort)messageType);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), payload.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), requestId);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (payload.Length > 0)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<InspectionPipeFrame> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[HeaderLength];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        if (!header.AsSpan(0, MagicBytes.Length).SequenceEqual(MagicBytes))
        {
            throw new InvalidDataException("Cabeçalho do Named Pipe possui identificação inválida.");
        }

        var protocolVersion = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4, 2));
        var messageTypeValue = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6, 2));
        if (!Enum.IsDefined((InspectionPipeMessageType)messageTypeValue))
        {
            throw new InvalidDataException($"Tipo de mensagem desconhecido: {messageTypeValue}.");
        }

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8, 4));
        if (payloadLength < 0 || payloadLength > MaxPayloadBytes)
        {
            throw new InvalidDataException("Tamanho de payload inválido no Named Pipe.");
        }

        var requestId = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12, 4));
        var payload = new byte[payloadLength];
        if (payloadLength > 0)
        {
            await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        }

        return new InspectionPipeFrame(
            protocolVersion,
            (InspectionPipeMessageType)messageTypeValue,
            requestId,
            payload);
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                throw new EndOfStreamException("O frame do Named Pipe terminou antes do tamanho declarado.");
            }

            offset += count;
        }
    }
}

/// <summary>Capacidades declaradas no <c>Hello</c> e no <c>HelloAck</c> (SPEC §7, novo na 1.1).</summary>
public static class InspectionCapabilities
{
    /// <summary>Cliente: aceita <c>CorrectionPush</c>. Servidor: pode enviar <c>CorrectionPush</c> nesta conexão.</summary>
    public const string Corrections = "corrections";

    /// <summary>Indica se a lista declara a capacidade (sem diferenciar maiúsculas). Lista nula = nada declarado.</summary>
    public static bool Declares(IEnumerable<string>? capabilities, string capability) =>
        capabilities is not null &&
        capabilities.Any(item => string.Equals(item, capability, StringComparison.OrdinalIgnoreCase));
}

public sealed class InspectionHello
{
    public string Application { get; set; } = "GeoAutoDocs";
    public string ApplicationVersion { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; } = InspectionPipeProtocol.CurrentVersion;
    public int InspectionSchemaVersion { get; set; }

    /// <summary>
    /// Novo na 1.1. Nulo = cliente 1.0 e o campo não é escrito no JSON; lista vazia = cliente 1.1 sem capacidades.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Capabilities { get; set; }
}

public sealed class InspectionHelloAck
{
    public string Application { get; set; } = string.Empty;
    public string ApplicationVersion { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; }
    public int InspectionSchemaVersion { get; set; }
    public string? CadProduct { get; set; }
    public string? CadVersion { get; set; }

    /// <summary>Novo na 1.1. Nulo = servidor 1.0 e o campo não é escrito no JSON.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Capabilities { get; set; }
}

public sealed class InspectionAcknowledgement
{
    public bool Accepted { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class InspectionPipeError
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
