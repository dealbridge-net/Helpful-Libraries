using Lombiq.HelpfulLibraries.Libraries.Contents;
using Newtonsoft.Json.Linq;
using OrchardCore.ContentManagement.Records;
using System;
using System.Threading.Tasks;
using YesSql;

namespace OrchardCore.ContentManagement
{
    public static class ContentExtensions
    {
        /// <summary>
        /// Gets a content part by its type.
        /// </summary>
        /// <returns>The content part or <see langword="null"/> if it doesn't exist.</returns>
        public static TPart As<TPart>(this IContent content)
            where TPart : ContentPart =>
            content.ContentItem.As<TPart>();

        /// <summary>
        /// Gets a content part by its type or create a new one.
        /// </summary>
        /// <typeparam name="TPart">The type of the content part.</typeparam>
        /// <returns>The content part instance or a new one if it doesn't exist.</returns>
        public static TPart GetOrCreate<TPart>(this IContent content)
            where TPart : ContentPart, new() =>
            content.ContentItem.GetOrCreate<TPart>();

        /// <summary>
        /// Adds a content part by its type.
        /// </summary>
        /// <typeparam name="TPart">The part to add to the <see cref="ContentItem"/>.</typeparam>
        /// <returns>The current <see cref="IContent"/> instance.</returns>
        public static IContent Weld<TPart>(this IContent content, TPart part)
            where TPart : ContentPart =>
            content.ContentItem.Weld(part);

        /// <summary>
        /// Updates the content part with the specified type.
        /// </summary>
        /// <typeparam name="TPart">The type of the part to update.</typeparam>
        /// <returns>The current <see cref="IContent"/> instance.</returns>
        public static IContent Apply<TPart>(this IContent content, TPart part)
            where TPart : ContentPart =>
            content.ContentItem.Apply(part);

        /// <summary>
        /// Modifies a new or existing content part by name.
        /// </summary>
        /// <param name="action">An action to apply on the content part.</param>
        /// <returns>The current <see cref="IContent"/> instance.</returns>
        public static IContent Alter<TPart>(this IContent content, Action<TPart> action)
            where TPart : ContentPart, new() =>
            content.ContentItem.Alter(action);

        /// <summary>
        /// Modifies a new or existing content part by name.
        /// </summary>
        /// <param name="action">An action to apply on the content part.</param>
        /// <typeparam name="TPart">The type of the part to update.</typeparam>
        /// <returns>The current <see cref="IContent"/> instance.</returns>
        public static async Task<IContent> AlterAsync<TPart>(this IContent content, Func<TPart, Task> action)
            where TPart : ContentPart, new() =>
            await content.ContentItem.AlterAsync(action);

        /// <summary>
        /// Merges properties to the contents of a content item.
        /// </summary>
        /// <param name="properties">The object to merge.</param>
        /// <param name="jsonMergeSettings">Settings for the merge.</param>
        /// <returns>The modified <see cref="ContentItem"/> instance.</returns>
        public static IContent Merge(this IContent content, object properties, JsonMergeSettings jsonMergeSettings = null) =>
            content.ContentItem.Merge(properties, jsonMergeSettings);

        /// <summary>
        /// Returns the <see cref="PublicationStatus"/> of the content item.
        /// </summary>
        /// <param name="content">The <see cref="IContent"/> whose <see cref="ContentItem"/> to check.</param>
        /// <returns>The status of the <see cref="ContentItem"/>'s publication if any.</returns>
        public static PublicationStatus GetPublicationStatus(this IContent content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (content.ContentItem == null)
            {
                throw new ArgumentNullException($"{nameof(content)}.{nameof(content.ContentItem)}");
            }

            if (content.ContentItem.Published) return PublicationStatus.Published;
            return content.ContentItem.Latest ? PublicationStatus.Draft : PublicationStatus.Deleted;
        }

        /// <summary>
        /// Prevents multiple "latest" versions in case somehow two threads edited the same <see cref="ContentItem"/> at
        /// the same time. For example this is possible if the update was done through XHR.
        /// </summary>
        /// <param name="content">The desired latest version of the content.</param>
        /// <remarks>
        /// <para>
        /// If the <paramref name="content"/> is not <see cref="ContentItem.Latest"/> nothing will happen. This is to
        /// prevent accidental deletion.
        /// </para>
        /// </remarks>
        public static async Task SanitizeContentItemVersionsAsync(this IContent content, ISession session)
        {
            if (!content.ContentItem.Latest) return;

            var contentItemId = content.ContentItem.ContentItemId;
            var contentItemVersionId = content.ContentItem.ContentItemVersionId;
            var stuckOtherDocuments = await session
                .Query<ContentItem, ContentItemIndex>(index =>
                    index.Latest &&
                    index.ContentItemId == contentItemId &&
                    index.ContentItemVersionId != contentItemVersionId)
                .ListAsync();

            foreach (var toRemove in stuckOtherDocuments)
            {
                toRemove.Published = false;
                toRemove.Latest = false;
                session.Save(toRemove);
            }
        }
    }
}
