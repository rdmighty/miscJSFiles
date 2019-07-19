using System;
using System.Collections.Generic;
using CounterpartProjects.Common;
using CounterpartProjects.PlanningTool;
using CounterpartProjects.PlanningTool.Abstraction;

namespace CounterpartProjects.PlanningTool.Implementor {
    public class PlanTasksUpdater {
        public List<IBasicPlanTask> planTasks;
        public PlanTasksUpdater (List<IBasicPlanTask> _planTasks) {
            this.planTasks = _planTasks;
        }

        private IDetailedPlanTask Detailed (IBasicPlanTask task) {
            if (task is IDetailedPlanTask)
                return (IDetailedPlanTask) (task);
            return null;
        }

        private void ResetTask (IBasicPlanTask task) {
            task.PlannedHours = 0;
            if (task is IDetailedPlanTask) {
                var _dTask = (IDetailedPlanTask) task;
                _dTask.Cost = 0;
                _dTask.ActualHours = 0;
                _dTask.StartDate = null;
                _dTask.FinishDate = null;
                _dTask.IsCompleted = false;
                _dTask.PercentComplete = 0;
                _dTask.ActualStartDate = null;
                _dTask.ActualFinishDate = null;
            }
        }

        private decimal GetPercentComplete (decimal percentSum, decimal plannedHours) {
            return Math.Floor (percentSum / (plannedHours > 0 ? plannedHours : 1));
        }

        private void UpdateCumulativeProperties (List<IBasicPlanTask> fromTasks, IBasicPlanTask toTask) {
            decimal percentSum = 0M, plannedHoursSum = 0M;
            foreach (var fromTask in fromTasks) {
                UpdateCumulativeProperties (fromTask, toTask);
                if (toTask is IDetailedPlanTask)
                    percentSum += (fromTask.PlannedHours * Detailed (fromTask).PercentComplete);

                if (!fromTask.IsMilestone && !fromTask.IsHeaderTask)
                    plannedHoursSum += fromTask.PlannedHours;
            }

            if (toTask is IDetailedPlanTask) {
                Detailed (toTask).PercentComplete = GetPercentComplete (percentSum, plannedHoursSum);
                Detailed (toTask).IsCompleted = Detailed (toTask).PercentComplete == 100;
            }
        }

        private void UpdateCumulativeProperties (IBasicPlanTask fromTask, IBasicPlanTask toTask) {
            var _dToTask = Detailed (toTask);
            var _dFromTask = Detailed (fromTask);

            toTask.PlannedHours += fromTask.PlannedHours;

            if (_dFromTask != null && _dToTask != null) {
                _dToTask.Cost += _dFromTask.Cost;
                _dToTask.ActualHours += _dFromTask.ActualHours;
                _dToTask.StartDate = GeneralUtility.Min (_dToTask.StartDate, _dFromTask.StartDate);
                _dToTask.FinishDate = GeneralUtility.Max (_dToTask.FinishDate, _dFromTask.FinishDate);
                _dToTask.ActualStartDate = GeneralUtility.Min (_dToTask.ActualStartDate, _dFromTask.ActualStartDate);
                _dToTask.ActualFinishDate = GeneralUtility.Min (_dToTask.ActualFinishDate, _dFromTask.ActualFinishDate);
            }
        }

        public PlanDetail RecalculateProperties () {
            var output = new PlanDetail ();
            IBasicPlanTask lastParent = null;
            IBasicPlanTask previousTask = null;
            var parents = new List<IBasicPlanTask> ();
            var milestoneChildren = new List<IBasicPlanTask> ();
            var parentIdMap = new Dictionary<long, IBasicPlanTask> ();
            var childrenCount = new Dictionary<long, int> ();

            for (int i = 0, max = this.planTasks.Count; i < max; i++) {
                var task = planTasks[i];

                if (!task.IsMilestone)
                    milestoneChildren.Add (task);

                if (previousTask != null) {
                    if (task.Indentation > previousTask.Indentation) {
                        parents.Add (previousTask);

                        lastParent = previousTask;
                        lastParent.IsHeaderTask = true;

                        // Reset parent's value
                        ResetTask (lastParent);
                    } else if (task.Indentation == previousTask.Indentation)
                        previousTask.IsHeaderTask = false;
                }

                if (lastParent != null && lastParent.Indentation >= task.Indentation) {
                    while (lastParent != null && lastParent.Indentation >= task.Indentation) {
                        parents.RemoveAt (parents.Count - 1);
                        if (parents.Count > 0)
                            lastParent = parents[parents.Count - 1];
                        else
                            lastParent = null;
                    }
                }

                int nChildren = 0;
                if (lastParent != null) {
                    if (lastParent.Indentation < task.Indentation) {
                        task.ParentId = lastParent.Id;
                        childrenCount.TryGetValue ((long) task.ParentId, out nChildren);
                        childrenCount[(long) task.ParentId] = ++nChildren;
                        task.Order = nChildren;
                        task.Indentation = lastParent.Indentation + 1;
                        task.TaskId = lastParent.TaskId + "." + task.Order;
                        parentIdMap[(long) task.ParentId] = lastParent;
                    } else {
                        parents.RemoveAt (parents.Count - 1);
                        lastParent = parents[parents.Count - 1];
                    }
                } else if (lastParent == null) {
                    task.ParentId = null;
                    childrenCount.TryGetValue (-1, out nChildren);
                    childrenCount[-1] = ++nChildren;
                    task.Order = nChildren;
                    task.Indentation = 0;
                    task.TaskId = task.Order.ToString ();
                }

                if (task.IsMilestone) {
                    // Reset milestone's value
                    ResetTask (task);
                    UpdateCumulativeProperties (milestoneChildren, task);
                }

                previousTask = task;
            }

            var percentSum = 0M;
            var percentDictionary = new Dictionary<long, decimal> ();
            for (int i = this.planTasks.Count - 1; i >= 0; i--) {
                var task = planTasks[i];
                if (!task.IsMilestone && !task.IsHeaderTask) {
                    if (task.ParentId != null) {
                        var parent = parentIdMap[(long) task.ParentId];
                        if (parent != null) {
                            UpdateCumulativeProperties (task, parent);

                            var _dTask = Detailed (task);
                            var _dParent = Detailed (parent);

                            if (_dTask != null && _dParent != null) {
                                var _percentSum = 0M;
                                percentDictionary.TryGetValue ((long) task.ParentId, out _percentSum);
                                percentDictionary[(long) task.ParentId] = (_percentSum += task.PlannedHours * _dTask.PercentComplete);
                                _dParent.PercentComplete = GetPercentComplete (_percentSum, parent.PlannedHours);
                                _dParent.IsCompleted = _dParent.PercentComplete == 100;
                            }
                        }
                    }

                    if (!task.IsMilestone && !task.IsHeaderTask)
                        output.PlannedHours += task.PlannedHours;

                    var _deTask = Detailed (task);
                    if (_deTask != null) {
                        output.EndDate = (DateTime) GeneralUtility.Max (_deTask.FinishDate, output.EndDate);
                        output.StartDate = (DateTime) GeneralUtility.Min (_deTask.StartDate, output.StartDate);

                        if (!task.IsMilestone && !task.IsHeaderTask)
                            percentSum += task.PlannedHours * _deTask.PercentComplete;
                    }
                }
            }

            output.PercentComplete = GetPercentComplete (percentSum, output.PlannedHours);
            return output;
        }

        public PlanDetail SortAndRecalculateProperties () {
            planTasks.Sort (new PlanTaskComparator<IBasicPlanTask> ());
            return RecalculateProperties ();
        }
    }
}
