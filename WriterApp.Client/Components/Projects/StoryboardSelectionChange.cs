using System;
using System.Collections.Generic;

namespace WriterApp.Client.Components.Projects
{
    public sealed record StoryboardSelectionChange(
        Guid? PrimarySceneId,
        IReadOnlyList<Guid> SelectedSceneIds);
}
