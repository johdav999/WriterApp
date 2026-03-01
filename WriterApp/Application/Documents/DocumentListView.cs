using System;

namespace WriterApp.Application.Documents
{
    public enum DocumentListView
    {
        Active,
        Archived,
        Trash
    }

    public static class DocumentListViewParser
    {
        public static DocumentListView Parse(string? view)
        {
            if (string.Equals(view, "archived", StringComparison.OrdinalIgnoreCase))
            {
                return DocumentListView.Archived;
            }

            if (string.Equals(view, "trash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(view, "trashed", StringComparison.OrdinalIgnoreCase))
            {
                return DocumentListView.Trash;
            }

            return DocumentListView.Active;
        }
    }
}
