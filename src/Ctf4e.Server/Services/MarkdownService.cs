using Markdig;

namespace Ctf4e.Server.Services
{
    public interface IMarkdownService
    {
        /// <summary>
        /// Renders the given Markdown string as HTML.
        /// </summary>
        /// <param name="markdown">Markdown string.</param>
        /// <returns></returns>
        string GetRenderedHtml(string markdown);
    }

    public class MarkdownService : IMarkdownService
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownService()
        {
            // Create new pipeline
            _pipeline = new MarkdownPipelineBuilder()
                .UseEmphasisExtras()
                .UseAutoIdentifiers()
                .UseAutoLinks()
                .UsePipeTables()
                .UseBootstrap()
                .DisableHtml() // Protect against XSS
                .Build();
        }

        /// <summary>
        /// Renders the given Markdown string as HTML.
        /// </summary>
        /// <param name="markdown">Markdown string.</param>
        /// <returns></returns>
        public string GetRenderedHtml(string markdown)
        {
            return Markdown.ToHtml(markdown, _pipeline);
        }
    }
}