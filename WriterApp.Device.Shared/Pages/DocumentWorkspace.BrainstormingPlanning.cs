using Microsoft.AspNetCore.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Device.Shared.Pages;

public partial class DocumentWorkspace
{
    [Inject] private LocalBrainstormingPlanningSaves BrainstormPlanning {get;set;}=default!;
    private async Task<BrainstormingPlanningSnapshot> CaptureBrainstormPlanningAsync(BrainstormingScope scope) {
        if(_brainstormContextScope!=scope || _session?.Document.DocumentId!=scope.DocumentId || !await BeforeContextWorkAsync())throw new InvalidOperationException("Save pending writing and resolve recovery before planning review.");
        try {await PrepareBrainstormDraftSend();return await BrainstormPlanning.CaptureAsync(scope);}
        finally{await AfterContextWorkAsync();}
    }
    private async Task<BrainstormingCache> RunBrainstormPlanningAsync(BrainstormingPlanningReview r,bool inspect,bool undo) {
        if(_brainstormContextScope!=r.Scope || _session?.Document.DocumentId!=r.Scope.DocumentId || !await BeforeContextWorkAsync())throw new InvalidOperationException("Open the original conversation and save pending writing before planning approval.");
        try {
            var flow=new BrainstormingPlanningWorkflow(BrainstormSessions);
            if(!inspect)return await flow.ApplyAsync(r.Scope,r,review=>BrainstormPlanning.ApplyAsync(review));
            var receipt=undo?await BrainstormPlanning.UndoAsync(r):await BrainstormPlanning.InspectAsync(r);
            return await flow.RememberAsync(r.Scope,r,receipt);
        }finally{await AfterContextWorkAsync();}
    }
    private Task<BrainstormingCache> ApplyBrainstormPlanningAsync(BrainstormingPlanningReview r)=>RunBrainstormPlanningAsync(r,false,false);
    private Task<BrainstormingCache> InspectBrainstormPlanningAsync(BrainstormingPlanningJournal j,bool undo)=>RunBrainstormPlanningAsync(j.Review,true,undo);
}
