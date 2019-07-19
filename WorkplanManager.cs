using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using CounterpartProjects.Common;
using CounterpartProjects.ListPlanningTool;
using CounterpartProjects.PlanningTool;
using CounterpartProjects.PlanningTool.Abstraction;
using CounterpartProjects.PlanningTool.Implementor;
using CounterpartProjects.Projects.Common;
using CounterpartProjects.Projects.ProjectPlanningTool;
using CounterpartProjects.Workplan.Implementor;
using Microsoft.EntityFrameworkCore;

namespace CounterpartProjects.Workplan {
    public class WorkplanManager : CounterpartProjectsDomainServiceBase {
        private readonly IRepository<WorkplanTask, long> _workplanTaskRepository;
        private readonly IRepository<ProjectplanTask, long> _projectplanTaskRepository;
        private readonly IRepository<WorkplanTaskAssignedUserMapping, long> _workplanTaskAssignedUserMappingRepository;
        private readonly IRepository<ProjectplanTaskAssignedUserMapping, long> _projectplanTaskAssignedUserMappingRepository;
        private readonly IRepository<WorkplanTaskPredecessorMapping, long> _workplanTaskPredecessorMappingRepository;
        private readonly IRepository<ProjectplanTaskPredecessorMapping, long> _projectplanTaskPredecessorMappingRepository;
        private readonly IRepository<StatusMaster, int> _statusRep;
        private readonly INumberSequenceManager _numberSequenceManager;
        private readonly IRepository<Workplan, long> _workplanRepository;
        private readonly IRepository<ListplanTask, long> _listplanTaskRepository;
        private readonly IRepository<ListplanTaskPredecessorMapping, long> _listplanTaskPredecessorMappingRepository;

        public WorkplanManager (IRepository<WorkplanTask, long> workplanTaskRepository,
            IRepository<ProjectplanTask, long> projectplanTaskRepository,
            IRepository<WorkplanTaskAssignedUserMapping, long> workplanTaskAssignedUserMappingRepository,
            IRepository<ProjectplanTaskAssignedUserMapping, long> projectplanTaskAssignedUserMappingRepository,
            IRepository<WorkplanTaskPredecessorMapping, long> workplanTaskPredecessorMappingRepository,
            IRepository<ProjectplanTaskPredecessorMapping, long> projectplanTaskPredecessorMappingRepository,
            IRepository<StatusMaster, int> statusRep,
            INumberSequenceManager numberSequenceManager,
            IRepository<Workplan, long> workplanRepository,
            IRepository<ListplanTask, long> listplanTaskRepository,
            IRepository<ListplanTaskPredecessorMapping, long> listplanTaskPredecessorMappingRepository) {
            _workplanTaskRepository = workplanTaskRepository;
            _projectplanTaskRepository = projectplanTaskRepository;
            _workplanTaskAssignedUserMappingRepository = workplanTaskAssignedUserMappingRepository;
            _projectplanTaskAssignedUserMappingRepository = projectplanTaskAssignedUserMappingRepository;
            _workplanTaskPredecessorMappingRepository = workplanTaskPredecessorMappingRepository;
            _projectplanTaskPredecessorMappingRepository = projectplanTaskPredecessorMappingRepository;
            _statusRep = statusRep;
            _numberSequenceManager = numberSequenceManager;
            _workplanRepository = workplanRepository;
            _listplanTaskRepository = listplanTaskRepository;
            _listplanTaskPredecessorMappingRepository = listplanTaskPredecessorMappingRepository;
        }

        private void Sort (List<WorkplanTask> workplanTasks) {
            workplanTasks.Sort (new PlanTaskComparator<WorkplanTask> ());
        }

        [UnitOfWork]
        public async Task ExportProjectplanToWorkplan (long projectplanId, long workplanId) {
            var oldToNewId = new Dictionary<long, long> ();
            var projectplanTasks = _projectplanTaskRepository.GetAll ().Where (p => p.ProjectplanId == projectplanId).ToList ();
            var workplanTasks = new List<WorkplanTask> ();

            // Sort so that we get parents first 
            projectplanTasks.Sort (new PlanTaskComparator<ProjectplanTask> ());

            foreach (var _pTask in projectplanTasks) {
                WorkplanTask newWorkplanTask = CopyProjectplanTaskToWorkplanTask (_pTask, new WorkplanTask (), oldToNewId);
                newWorkplanTask.TaskNumber = await _numberSequenceManager.GetLatestSequenceNumberForType (NumberSequenceType.Task);
                newWorkplanTask.WorkplanId = workplanId;

                var id = await _workplanTaskRepository.InsertAndGetIdAsync (newWorkplanTask);

                oldToNewId.Add (_pTask.Id, id);
                workplanTasks.Add (newWorkplanTask);
            }

            /**
             * We have to traverse projectplanTasks once again to create
             * WorkplanTaskAssignedUserMappin and WorkplanTaskPredecessorMapping.
             * We are traversing again because we wanted oldToNewId dictionary to have
             * mapping from oldId to newId of all the tasks.
             */
            var allProjectIds = projectplanTasks.Select (p => p.Id);
            foreach (var projectId in allProjectIds) {
                var assignedUserMappings = _projectplanTaskAssignedUserMappingRepository.GetAll ().Where (m => m.ProjectplanTaskId == projectId).ToList ();
                var predecessorMappings = _projectplanTaskPredecessorMappingRepository.GetAll ().Where (m => m.ProjectplanTaskId == projectId).ToList ();

                foreach (var userMapping in assignedUserMappings) {
                    if (userMapping.UserId.HasValue)
                        await CopyAssignedUserMapping (userMapping, oldToNewId);
                }

                foreach (var predMapping in predecessorMappings)
                    await CopyPredecessorMapping (predMapping, oldToNewId);
            }
        }

        /**
         * This function is called only if AssignedUserMapping's UserId has value
         */
        public async Task CopyAssignedUserMapping (ProjectplanTaskAssignedUserMapping _pUserMapping, Dictionary<long, long> oldToNewId) {
            var newWorkplanTaskAssignedUserMapping = new WorkplanTaskAssignedUserMapping ();

            newWorkplanTaskAssignedUserMapping.WorkplanTaskId = oldToNewId[_pUserMapping.ProjectplanTaskId];
            newWorkplanTaskAssignedUserMapping.UserId = (long) _pUserMapping.UserId;

            await _workplanTaskAssignedUserMappingRepository.InsertAsync (newWorkplanTaskAssignedUserMapping);
        }

        public async Task CopyPredecessorMapping (ProjectplanTaskPredecessorMapping _pMapping, Dictionary<long, long> oldToNewId) {
            var newWorkplanTaskPredecessorMapping = new WorkplanTaskPredecessorMapping ();

            newWorkplanTaskPredecessorMapping.WorkplanTaskId = oldToNewId[_pMapping.ProjectplanTaskId];
            newWorkplanTaskPredecessorMapping.WorkplanPredecessorTaskId = oldToNewId[_pMapping.ProjectplanPredecessorTaskId];

            await _workplanTaskPredecessorMappingRepository.InsertAsync (newWorkplanTaskPredecessorMapping);
        }

        private WorkplanTask CopyProjectplanTaskToWorkplanTask (ProjectplanTask projectplanTask, WorkplanTask workplanTask, Dictionary<long, long> oldToNewId) {
            workplanTask.Title = projectplanTask.Title;
            workplanTask.StartDate = projectplanTask.StartDate;
            workplanTask.FinishDate = projectplanTask.FinishDate;
            workplanTask.PlannedHours = projectplanTask.PlannedHours;
            workplanTask.ActualStartDate = projectplanTask.ActualStartDate;
            workplanTask.ActualFinishDate = projectplanTask.ActualFinishDate;
            workplanTask.ActualHours = projectplanTask.ActualHours;
            workplanTask.Cost = projectplanTask.Cost;
            workplanTask.ResourceTypeId = projectplanTask.ResourceTypeId;
            workplanTask.IsExpanded = projectplanTask.IsExpanded;
            workplanTask.IsMilestone = projectplanTask.IsMilestone;
            workplanTask.IsHeaderTask = projectplanTask.IsHeaderTask;
            workplanTask.Indentation = projectplanTask.Indentation;
            workplanTask.Order = projectplanTask.Order;
            workplanTask.ParentId = projectplanTask.ParentId.HasValue ? oldToNewId[(long) projectplanTask.ParentId] : projectplanTask.ParentId;
            workplanTask.TaskId = projectplanTask.TaskId;

            return workplanTask;
        }

        // Copying data from one workplan to another 

        public async Task ImportWorkplan (long fromWorkplanId, long toWorkplanId) {
            // Copy all tasks and mappings from workplan to projectplan
            await CopyWorkplan (fromWorkplanId, toWorkplanId, null, null);
        }

        private async Task CopyWorkplan (long fromWorkplanId, long toWorkplanId, TemplateMergeOffset? offset, string taskId) {
            var allWorkplanEntities = new WorkplanEntities (toWorkplanId);

            // From WorkplanEntities
            var fromWorkplanEntities = new WorkplanEntities (fromWorkplanId);
            fromWorkplanEntities.Tasks = await _workplanTaskRepository.GetAll ().Where (w => w.WorkplanId == fromWorkplanId).ToListAsync ();
            fromWorkplanEntities.Tasks.Sort (new PlanTaskComparator<WorkplanTask> ());
            var idsWorkplanTasks = fromWorkplanEntities.Tasks.Select (p => p.Id);
            fromWorkplanEntities.UserMappings = await _workplanTaskAssignedUserMappingRepository.GetAll ().Where (m => idsWorkplanTasks.Contains (m.WorkplanTaskId)).ToListAsync ();
            fromWorkplanEntities.PredecessorMappings = await _workplanTaskPredecessorMappingRepository.GetAll ().Where (m => idsWorkplanTasks.Contains (m.WorkplanTaskId)).ToListAsync ();

            // To Workplan Entities; we do not need user mappings of ToWorkplan
            var toWorkplanEntities = new WorkplanEntities (toWorkplanId);
            toWorkplanEntities.Tasks = await _workplanTaskRepository.GetAll ().Where (w => w.WorkplanId == toWorkplanId).ToListAsync ();
            toWorkplanEntities.Tasks.Sort (new PlanTaskComparator<WorkplanTask> ());
            idsWorkplanTasks = toWorkplanEntities.Tasks.Select (p => p.Id);
            toWorkplanEntities.PredecessorMappings = await _workplanTaskPredecessorMappingRepository.GetAll ().Where (m => idsWorkplanTasks.Contains (m.WorkplanTaskId)).ToListAsync ();

            // Merge plans
            PlanMergerFactory.GetMerger (fromWorkplanEntities, toWorkplanEntities, allWorkplanEntities, offset, taskId).Merge ();

            // Update all workplan entities
            await UpdateAllNewWorkplanTasks(allWorkplanEntities);
        }

        private async Task UpdateAllNewWorkplanTasks (WorkplanEntities allWorkplanEntities) {
            // Save new tasks
            var oldToNewIds = new Dictionary<long, long> ();
            foreach (var task in allWorkplanEntities.Tasks) {
                if (task.Id <= 0) {
                    var initialId = task.Id;
                    task.TaskNumber = await _numberSequenceManager.GetLatestSequenceNumberForType (NumberSequenceType.Task);
                    if (task.ParentId != null || task.ParentId <= 0)
                        task.ParentId = oldToNewIds[(long) task.ParentId];
                    var newId = await _workplanTaskRepository.InsertAndGetIdAsync (task);
                    oldToNewIds[initialId] = newId;
                }
            }

            // Update new user mappings
            foreach (var userMapping in allWorkplanEntities.UserMappings) {
                if (userMapping.WorkplanTaskId <= 0)
                    userMapping.WorkplanTaskId = oldToNewIds[userMapping.WorkplanTaskId];

            }

            // Update new predecessor mappings
            foreach (var predecessorMapping in allWorkplanEntities.PredecessorMappings) {
                if (predecessorMapping.WorkplanTaskId <= 0)
                    predecessorMapping.WorkplanTaskId = oldToNewIds[predecessorMapping.WorkplanTaskId];
                if (predecessorMapping.WorkplanPredecessorTaskId <= 0)
                    predecessorMapping.WorkplanPredecessorTaskId = oldToNewIds[predecessorMapping.WorkplanPredecessorTaskId];
            }
        }

        private async Task<WorkplanTaskAssignedUserMapping> CopyAssignedUserMapping (WorkplanTaskAssignedUserMapping _wUserMapping, Dictionary<long, long> oldToNewId) {
            var newWorkplanTaskAssignedUserMapping = new WorkplanTaskAssignedUserMapping ();
            newWorkplanTaskAssignedUserMapping.WorkplanTaskId = oldToNewId[_wUserMapping.WorkplanTaskId];
            newWorkplanTaskAssignedUserMapping.UserId = (long) _wUserMapping.UserId;
            await _workplanTaskAssignedUserMappingRepository.InsertAsync (newWorkplanTaskAssignedUserMapping);
            return newWorkplanTaskAssignedUserMapping;
        }

        private async Task<WorkplanTaskPredecessorMapping> CopyPredecessorMapping (WorkplanTaskPredecessorMapping _wMapping, Dictionary<long, long> oldToNewId) {
            var newWorkplanTaskPredecessorMapping = new WorkplanTaskPredecessorMapping ();
            newWorkplanTaskPredecessorMapping.WorkplanTaskId = oldToNewId[_wMapping.WorkplanTaskId];
            newWorkplanTaskPredecessorMapping.WorkplanPredecessorTaskId = oldToNewId[_wMapping.WorkplanPredecessorTaskId];
            await _workplanTaskPredecessorMappingRepository.InsertAsync (newWorkplanTaskPredecessorMapping);
            return newWorkplanTaskPredecessorMapping;
        }

        public async Task<WorkplanTaskPredecessorMapping> CopyPredecessorMapping (ListplanTaskPredecessorMapping _lMapping, Dictionary<long, long> oldToNewId) {
            var newWorkplanTaskPredecessorMapping = new WorkplanTaskPredecessorMapping ();
            newWorkplanTaskPredecessorMapping.WorkplanTaskId = oldToNewId[_lMapping.ListplanTaskId];
            newWorkplanTaskPredecessorMapping.WorkplanPredecessorTaskId = oldToNewId[_lMapping.ListplanPredecessorTaskId];
            await _workplanTaskPredecessorMappingRepository.InsertAsync (newWorkplanTaskPredecessorMapping);
            return newWorkplanTaskPredecessorMapping;
        }

        [UnitOfWork]
        public async Task MergeTemplateInWorkplan (long planId, long workplanId, string planType, TemplateMergeOffset offset, string taskId) {
            if (planType == "Workplan") {
                await CopyWorkplan (planId, workplanId, offset, taskId);
            } else if (planType == "Tasklist") {
                await CopyListplanToWorkplan (planId, workplanId, offset, taskId);
            }
        }

        private async Task CopyListplanToWorkplan (long fromListplanId, long toWorkplanId, TemplateMergeOffset? offset, string taskId) {
            var allWorkplanEntities = new WorkplanEntities (toWorkplanId);

            // From WorkplanEntities
            var fromListplanEntities = new WorkplanEntities (fromListplanId);
            fromListplanEntities.Tasks = await _workplanTaskRepository.GetAll ().Where (w => w.WorkplanId == fromListplanId).ToListAsync ();
            fromListplanEntities.Tasks.Sort (new PlanTaskComparator<WorkplanTask> ());
            var idsWorkplanTasks = fromListplanEntities.Tasks.Select (p => p.Id);            
            fromListplanEntities.PredecessorMappings = await _workplanTaskPredecessorMappingRepository.GetAll ().Where (m => idsWorkplanTasks.Contains (m.WorkplanTaskId)).ToListAsync ();

            // To Workplan Entities; we do not need user mappings of ToWorkplan
            var toWorkplanEntities = new WorkplanEntities (toWorkplanId);
            toWorkplanEntities.Tasks = await _workplanTaskRepository.GetAll ().Where (w => w.WorkplanId == toWorkplanId).ToListAsync ();
            toWorkplanEntities.Tasks.Sort (new PlanTaskComparator<WorkplanTask> ());
            idsWorkplanTasks = toWorkplanEntities.Tasks.Select (p => p.Id);
            toWorkplanEntities.PredecessorMappings = await _workplanTaskPredecessorMappingRepository.GetAll ().Where (m => idsWorkplanTasks.Contains (m.WorkplanTaskId)).ToListAsync ();

            // Merge plans
            PlanMergerFactory.GetMerger (fromListplanEntities, toWorkplanEntities, allWorkplanEntities, offset, taskId).Merge ();

            // Update all workplan entities
            await UpdateAllNewWorkplanTasks(allWorkplanEntities);
        }
    }
}
