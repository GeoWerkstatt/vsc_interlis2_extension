using Geowerkstatt.Interlis.LanguageServer.Visitors;
using Geowerkstatt.Interlis.Tools;
using Geowerkstatt.Interlis.Tools.AST;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Geowerkstatt.Interlis.LanguageServer.Handlers;

/// <summary>
/// Command handler to generate diagram document for an INTERLIS file.
/// Responds to workspace.executeCommand requests using the executeCommandProvider capability of the language server protocol.
/// </summary>
public class GenerateDiagramHandler : ExecuteTypedResponseCommandHandlerBase<GenerateDiagramOptions, string?>
{
    public const string Command = "generateDiagram";

    private readonly ILogger<GenerateDiagramHandler> logger;
    private readonly InterlisReader interlisReader;
    private readonly FileContentCache fileContentCache;

    public GenerateDiagramHandler(ILogger<GenerateDiagramHandler> logger, InterlisReader interlisReader, FileContentCache fileContentCache, ISerializer serializer) : base(Command, serializer)
    {
        this.logger = logger;
        this.interlisReader = interlisReader;
        this.fileContentCache = fileContentCache;
    }

    /// <summary>
    /// Handles the generateDiagram requests.
    /// </summary>
    /// <param name="options">The requested options.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <returns>The generated diagram document, or <c>null</c> if the INTERLIS file was not found.</returns>
    public override Task<string?> Handle(GenerateDiagramOptions options, CancellationToken cancellationToken)
    {
        var uri = options.Uri;
        var fileContent = uri == null ? null : fileContentCache.GetBuffer(uri);
        if (string.IsNullOrEmpty(fileContent))
        {
            return Task.FromResult<string?>(null);
        }

        logger.LogInformation("Generate diagram for {0}", uri);

        using var stringReader = new StringReader(fileContent);
        var interlisFile = interlisReader.ReadFile(stringReader);
        var diagram = GenerateDiagram(interlisFile);

        return Task.FromResult<string?>(diagram);
    }

    private static string GenerateDiagram(InterlisFile interlisFile)
    {
        var visitor = new DiagramDocumentVisitor();
        visitor.VisitInterlisFile(interlisFile);
        return visitor.GetDiagramDocument();
    }
}
