using System;
using System.Text;
using WriterApp.Domain.Documents;
using SynopsisModel = WriterApp.Domain.Documents.Synopsis;

namespace WriterApp.Application.Synopsis
{
    public sealed class SynopsisAiContextBuilder
    {
        private const string EmptyValuePlaceholder = "(not defined yet)";

        public string Build(SynopsisModel synopsis)
        {
            if (synopsis is null)
            {
                throw new ArgumentNullException(nameof(synopsis));
            }

            StringBuilder builder = new();
            builder.AppendLine("Synopsis Context");
            builder.AppendLine();

            foreach (SynopsisFieldDefinition field in SynopsisFieldCatalog.Fields)
            {
                builder.AppendLine($"{field.Label}:");
                if (!SynopsisFieldCatalog.TryGetValue(synopsis, field.Key, out string value)
                    || string.IsNullOrWhiteSpace(value))
                {
                    builder.AppendLine(EmptyValuePlaceholder);
                }
                else
                {
                    builder.AppendLine(value);
                }

                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }
    }
}
