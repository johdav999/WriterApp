using System;
using System.Collections.Generic;

namespace WriterApp.UI.Shared.Projects
{
    public sealed record StoryboardSelectionChange(
        Guid? PrimarySceneId,
        IReadOnlyList<Guid> SelectedSceneIds);
}
