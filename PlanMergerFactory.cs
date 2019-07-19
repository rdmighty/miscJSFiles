using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CounterpartProjects.ListPlanningTool;
using CounterpartProjects.Projects.ProjectPlanningTool;
using CounterpartProjects.Workplan;

namespace CounterpartProjects.PlanningTool.Abstraction {
    public class PlanMergerFactory {        
        public static IPlanMerger GetMerger(ListplanEntities listplanEntities, WorkplanEntities workplanEntities, WorkplanEntities allWorkplanEntities, TemplateMergeOffset? offset, string taskId) {
            return new ListplanToWorkplanMerger(listplanEntities, workplanEntities, allWorkplanEntities, offset, taskId);
        }

        public static IPlanMerger GetMerger(WorkplanEntities newEntities, WorkplanEntities workplanEntities, WorkplanEntities allWorkplanEntities, TemplateMergeOffset? offset, string taskId) {
            return new WorkplanToWorkplanMerger(newEntities, workplanEntities, allWorkplanEntities, offset, taskId);
        }
    }
}
