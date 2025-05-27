using System;

namespace ClioDataMigrator.Models
{
    /// <summary>
    /// Represents information about a document from a legacy system that needs to be migrated to Clio.
    /// </summary>
    public class LegacyDocumentInfo
    {
        /// <summary>
        /// Gets or sets the desired name for the document in Clio.
        /// </summary>
        public string DesiredDocumentName { get; set; }

        /// <summary>
        /// Gets or sets the path to the file on disk.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets a description for the document.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the case/matter in the legacy system.
        /// This will be used to find the corresponding matter in Clio.
        /// </summary>
        public string LegacyCaseIdentifier { get; set; }

        /// <summary>
        /// Gets or sets the document type in the legacy system.
        /// </summary>
        public string DocumentType { get; set; }

        /// <summary>
        /// Gets or sets the date the document was created in the legacy system.
        /// </summary>
        public DateTime? DateCreated { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the client in the legacy system.
        /// </summary>
        public string LegacyClientIdentifier { get; set; }

        /// <summary>
        /// Gets or sets any additional metadata associated with the document.
        /// </summary>
        public string AdditionalMetadata { get; set; }
    }
}
