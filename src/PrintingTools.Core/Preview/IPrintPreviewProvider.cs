using System.Threading;
using System.Threading.Tasks;
using PrintingTools.Core;

namespace PrintingTools.Core.Preview;

/// <summary>
/// Describes a component capable of generating print preview models for a given session.
/// </summary>
public interface IPrintPreviewProvider
{
    /// <summary>
    /// Creates a preview model for the supplied session.
    /// </summary>
    /// <param name="session">The active print session.</param>
    /// <param name="cancellationToken">Token used to cancel the preview operation.</param>
    Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default);
}
