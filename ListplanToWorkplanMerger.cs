using System;
using System.Collections.Generic;
using System.Linq;
using Abp.UI;
using CounterpartProjects.Common;
using CounterpartProjects.ListPlanningTool;
using CounterpartProjects.PlanningTool;
using CounterpartProjects.PlanningTool.Abstraction;
using CounterpartProjects.PlanningTool.Implementor;
using CounterpartProjects.Workplan;

namespace CounterpartProjects.Projects.ProjectPlanningTool {
    public class ListplanToWorkplanMerger : IPlanMerger {
        static int Id = 1;
        private WorkplanEntities _newWorkplanEntities;

        string _taskId;
        TemplateMergeOffset? _offset;
        WorkplanEntities _toWorkplanEntities;
        ListplanEntities _fromListplanEntities;
        WorkplanEntities _allWorkplanEntities;
        public ListplanToWorkplanMerger (ListplanEntities fromListplanEntities, WorkplanEntities toWorkplanEntities, WorkplanEntities allWorkplanEntities, TemplateMergeOffset? offset, string taskId) {
            _taskId = taskId;
            _offset = offset;
            _toWorkplanEntities = toWorkplanEntities;
            _allWorkplanEntities = allWorkplanEntities;
            _fromListplanEntities = fromListplanEntities;
            _newWorkplanEntities = new WorkplanEntities (toWorkplanEntities.WorkplanId);
        }

        public void Merge () {
            CopyWorkplanToExistingWorkplan ();

            if (_offset != null) {
                switch (_offset) {
                    case TemplateMergeOffset.Between:
                        this.MergeInBetween ();
                        break;
                    case TemplateMergeOffset.Start:
                        this.MergeAtStart ();
                        break;
                    case TemplateMergeOffset.End:
                        this.MergeAtEnd ();
                        break;
                }
            } else {
                _allWorkplanEntities.Tasks = _newWorkplanEntities.Tasks.ToList ();
                _allWorkplanEntities.UserMappings = _newWorkplanEntities.UserMappings;
                _allWorkplanEntities.PredecessorMappings = _newWorkplanEntities.PredecessorMappings;
            }

            // Update navigation and cumulative properties
            new PlanTasksUpdater (_allWorkplanEntities.Tasks.ConvertAll (p => (IBasicPlanTask) p)).RecalculateProperties ();
        }

        private WorkplanTask CopyToNewWorkplan (ListplanTask fromListplanTask, Dictionary<long, long> oldToNewId) {
            var newWorkplanTask = new WorkplanTask ();

            newWorkplanTask.Id = ListplanToWorkplanMerger.Id++;
            newWorkplanTask.Title = fromListplanTask.Title;
            newWorkplanTask.Order = fromListplanTask.Order;
            newWorkplanTask.TaskId = fromListplanTask.TaskId;
            newWorkplanTask.IsExpanded = fromListplanTask.IsExpanded;
            newWorkplanTask.IsMilestone = fromListplanTask.IsMilestone;
            newWorkplanTask.Indentation = fromListplanTask.Indentation;
            newWorkplanTask.PlannedHours = fromListplanTask.PlannedHours;
            newWorkplanTask.IsHeaderTask = fromListplanTask.IsHeaderTask;
            newWorkplanTask.ResourceTypeId = fromListplanTask.ResourceTypeId;
            newWorkplanTask.ParentId = fromListplanTask.ParentId.HasValue ? oldToNewId[(long) fromListplanTask.ParentId] : fromListplanTask.ParentId;

            return newWorkplanTask;
        }

        private WorkplanTaskPredecessorMapping CopyPredecessorMapping (ListplanTaskPredecessorMapping _wMapping, Dictionary<long, long> oldToNewId) {
            var newWorkplanTaskPredecessorMapping = new WorkplanTaskPredecessorMapping ();
            newWorkplanTaskPredecessorMapping.WorkplanTaskId = oldToNewId[_wMapping.ListplanTaskId];
            newWorkplanTaskPredecessorMapping.WorkplanPredecessorTaskId = oldToNewId[_wMapping.ListplanPredecessorTaskId];
            return newWorkplanTaskPredecessorMapping;
        }

        private void CopyWorkplanToExistingWorkplan () {
            var oldToNewId = new Dictionary<long, long> ();

            foreach (var task in _fromListplanEntities.Tasks) {
                var newWorkplanTask = CopyToNewWorkplan (task, oldToNewId);
                newWorkplanTask.WorkplanId = _toWorkplanEntities.WorkplanId;
                oldToNewId.Add (task.Id, newWorkplanTask.Id);

                // Push it into the List
                _newWorkplanEntities.Tasks.Add (newWorkplanTask);
            }

            var allListplanTaskIds = _fromListplanEntities.Tasks.Select (p => p.Id);
            foreach (var ListplanTaskId in allListplanTaskIds) {
                var _predecessorMappings = _fromListplanEntities.PredecessorMappings.FindAll (m => m.ListplanTaskId == ListplanTaskId);

                foreach (var predMapping in _predecessorMappings)
                    _newWorkplanEntities.PredecessorMappings.Add (CopyPredecessorMapping (predMapping, oldToNewId));
            }
        }

        private DateTime? GetOffsettedDate (DateTime? date, double offset) {
            if (date != null) {
                var newDate = ((DateTime) date).AddDays (-offset);

                // Get working day
                while (newDate.DayOfWeek == DayOfWeek.Sunday || newDate.DayOfWeek == DayOfWeek.Saturday)
                    newDate = newDate.AddDays (1);

                return newDate;
            } else
                return date;
        }

        private void MergeInBetween () {
            var index = _toWorkplanEntities.Tasks.FindIndex (t => t.TaskId == _taskId);

            if (index == -1)
                throw new UserFriendlyException ("Invalid Task ID");

            // Create a copy
            _allWorkplanEntities.Tasks = _toWorkplanEntities.Tasks.ToList ();
            _allWorkplanEntities.UserMappings = _newWorkplanEntities.UserMappings;
            _allWorkplanEntities.PredecessorMappings = _newWorkplanEntities.PredecessorMappings;

            var taskWithTaskId = _toWorkplanEntities.Tasks[index];
            var firstNewTask = _newWorkplanEntities.Tasks.Count > 0 ? _newWorkplanEntities.Tasks[0] : null;

            if (firstNewTask != null) {
                var indentationDiff = taskWithTaskId.Indentation - firstNewTask.Indentation;
                var nMinDate = PlanTasksUtility.GetMinDate (_newWorkplanEntities.Tasks.ConvertAll (p => (ISpannedPlanTask) p));
                var dateOffset = GeneralUtility.Diff2 ((DateTime) nMinDate, (DateTime) taskWithTaskId.StartDate);

                // Alter dates in accordance with the dateoffset; Predecessor rule is not respected
                foreach (var task in this._newWorkplanEntities.Tasks) {
                    task.StartDate = GetOffsettedDate (task.StartDate, dateOffset);
                    task.FinishDate = GetOffsettedDate (task.FinishDate, dateOffset);
                    task.ActualStartDate = GetOffsettedDate (task.ActualStartDate, dateOffset);
                    task.ActualFinishDate = GetOffsettedDate (task.ActualFinishDate, dateOffset);
                    task.Indentation = task.Indentation + indentationDiff;
                }

                // Merge it
                _allWorkplanEntities.Tasks.InsertRange (index, _newWorkplanEntities.Tasks);
            }
        }

        private void MergeAtStart () {
            // Create a copy 
            _allWorkplanEntities.UserMappings = _newWorkplanEntities.UserMappings;
            _allWorkplanEntities.PredecessorMappings = _newWorkplanEntities.PredecessorMappings;

            var nMinDate = PlanTasksUtility.GetMinDate (_newWorkplanEntities.Tasks.ConvertAll (p => (ISpannedPlanTask) p));
            var eMinDate = PlanTasksUtility.GetMinDate (_toWorkplanEntities.Tasks.ConvertAll (p => (ISpannedPlanTask) p));
            var dateOffset = GeneralUtility.Diff2 ((DateTime) nMinDate, (DateTime) eMinDate);

            // Alter dates in accordance with the dateoffset; Predecessor rule is not respected
            DateTime? _maxDate = null;
            foreach (var task in _newWorkplanEntities.Tasks) {
                task.StartDate = GetOffsettedDate (task.StartDate, dateOffset);
                task.FinishDate = GetOffsettedDate (task.FinishDate, dateOffset);
                task.ActualStartDate = GetOffsettedDate (task.ActualStartDate, dateOffset);
                task.ActualFinishDate = GetOffsettedDate (task.ActualFinishDate, dateOffset);
                _maxDate = GeneralUtility.Max (_maxDate, task.StartDate);
            }

            if (_maxDate != null) {
                dateOffset = GeneralUtility.Diff2 ((DateTime) eMinDate, (DateTime) _maxDate);

                foreach (var task in _toWorkplanEntities.Tasks) {
                    task.StartDate = GetOffsettedDate (task.StartDate, dateOffset);
                    task.FinishDate = GetOffsettedDate (task.FinishDate, dateOffset);
                    task.ActualStartDate = GetOffsettedDate (task.ActualStartDate, dateOffset);
                    task.ActualFinishDate = GetOffsettedDate (task.ActualFinishDate, dateOffset);
                }
            }

            // Merge both the array
            _allWorkplanEntities.Tasks = _newWorkplanEntities.Tasks.ToList ();
            _allWorkplanEntities.Tasks.AddRange (_toWorkplanEntities.Tasks);
        }

        private void MergeAtEnd () {
            // Create a copy 
            _allWorkplanEntities.UserMappings = _newWorkplanEntities.UserMappings;
            _allWorkplanEntities.PredecessorMappings = _newWorkplanEntities.PredecessorMappings;

            var nMinDate = PlanTasksUtility.GetMinDate (_newWorkplanEntities.Tasks.ConvertAll (p => (ISpannedPlanTask) p));
            var eMaxDate = PlanTasksUtility.GetMaxDate (_toWorkplanEntities.Tasks.ConvertAll (p => (ISpannedPlanTask) p));
            var dateOffset = GeneralUtility.Diff2 ((DateTime) nMinDate, (DateTime) eMaxDate);

            // Alter dates in accordance with the dateoffset; Predecessor rule is not respected
            foreach (var task in _newWorkplanEntities.Tasks) {
                task.StartDate = GetOffsettedDate (task.StartDate, dateOffset);
                task.FinishDate = GetOffsettedDate (task.FinishDate, dateOffset);
                task.ActualStartDate = GetOffsettedDate (task.ActualStartDate, dateOffset);
                task.ActualFinishDate = GetOffsettedDate (task.ActualFinishDate, dateOffset);
            }

            // Merge both the array
            _allWorkplanEntities.Tasks = _toWorkplanEntities.Tasks.ToList ();
            _allWorkplanEntities.Tasks.AddRange (_newWorkplanEntities.Tasks);
        }
    }
}
