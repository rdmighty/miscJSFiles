using System;
using System.Collections.Generic;
using CounterpartProjects.Common;
using CounterpartProjects.PlanningTool;
using CounterpartProjects.PlanningTool.Abstraction;

namespace CounterpartProjects.PlanningTool.Implementor {
    public class PlanTasksUpdater : PlanTasks {
        public PlanTasksUpdater (List<IBasicPlanTask> planTasks) : base (planTasks) {

        }

        private void ResetTask (IBasicPlanTask task) {
            task.PlannedHours = 0;
            if (task is IPlanTaskWithCost) ((IPlanTaskWithCost) task).Cost = 0;

            if (task is ISpannedPlanTask) {
                ((ISpannedPlanTask) task).StartDate = null;
                ((ISpannedPlanTask) task).FinishDate = null;
            }

            if (task is IActualAuditedPlanTask) {
                ((IActualAuditedPlanTask) task).ActualHours = 0;
                ((IActualAuditedPlanTask) task).ActualFinishDate = null;
                ((IActualAuditedPlanTask) task).ActualStartDate = null;
            }

            if (task is IProgressAuditedPlanTask) {
                ((IProgressAuditedPlanTask) task).IsCompleted = false;
                ((IProgressAuditedPlanTask) task).PercentComplete = 0;
            }
        }

        private void UpdateCumulativeProperties (List<IBasicPlanTask> fromTasks, IBasicPlanTask toTask) {
            decimal percentSum = 0M, plannedHoursSum = 0M;
            foreach (var fromTask in fromTasks) {
                UpdateCumulativeProperties (fromTask, toTask);
                if (toTask is IProgressAuditedPlanTask)
                    percentSum += (fromTask.PlannedHours * ((IProgressAuditedPlanTask) fromTask).PercentComplete);
            }

            if (toTask is IProgressAuditedPlanTask) {
                ((IProgressAuditedPlanTask) toTask).PercentComplete = Math.Floor (percentSum / plannedHoursSum);
                ((IProgressAuditedPlanTask) toTask).IsCompleted = ((IProgressAuditedPlanTask) toTask).PercentComplete == 100;
            }
        }

        private void UpdateCumulativeProperties (IBasicPlanTask fromTask, IBasicPlanTask toTask) {
            toTask.PlannedHours += fromTask.PlannedHours;
            if (toTask is IPlanTaskWithCost) ((IPlanTaskWithCost) toTask).Cost += ((IPlanTaskWithCost) fromTask).Cost;
            if (toTask is ISpannedPlanTask) {
                ((ISpannedPlanTask) toTask).StartDate = GeneralUtility.Min (((ISpannedPlanTask) toTask).StartDate, ((ISpannedPlanTask) fromTask).StartDate);
                ((ISpannedPlanTask) toTask).FinishDate = GeneralUtility.Max (((ISpannedPlanTask) toTask).FinishDate, ((ISpannedPlanTask) fromTask).FinishDate);
            }
            if (toTask is IActualAuditedPlanTask) {
                ((IActualAuditedPlanTask) toTask).ActualHours += ((IActualAuditedPlanTask) fromTask).ActualHours;
                ((IActualAuditedPlanTask) toTask).ActualStartDate = GeneralUtility.Min (((IActualAuditedPlanTask) toTask).ActualStartDate, ((IActualAuditedPlanTask) fromTask).ActualStartDate);
                ((IActualAuditedPlanTask) toTask).ActualFinishDate = GeneralUtility.Min (((IActualAuditedPlanTask) toTask).ActualFinishDate, ((IActualAuditedPlanTask) fromTask).ActualFinishDate);
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
                        lastParent = parents[parents.Count - 1];
                    }
                }

                int nChildren = 0;
                if (lastParent != null) {
                    if (lastParent.Indentation < task.Indentation) {
                        task.ParentId = lastParent.Id;
                        childrenCount.TryGetValue ((long) task.ParentId, out nChildren);
                        childrenCount[(long) task.ParentId] = nChildren++;
                        task.Order = nChildren;
                        task.Indentation = lastParent.Indentation + 1;
                        task.TaskId = lastParent.TaskId + "." + task.Order;
                        parentIdMap.Add ((long) task.ParentId, lastParent);
                    } else {
                        parents.RemoveAt (parents.Count - 1);
                        lastParent = parents[parents.Count - 1];
                    }
                } else if (lastParent == null) {
                    task.ParentId = null;
                    childrenCount.TryGetValue (-1, out nChildren);
                    childrenCount[-1] = nChildren++;
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
                if (task.ParentId != null) {
                    var parent = parentIdMap[(long) task.ParentId];
                    if (parent != null) {
                        UpdateCumulativeProperties (task, parent);

                        if (task is IProgressAuditedPlanTask) {
                            var _percentSum = 0M;
                            percentDictionary.TryGetValue ((long) task.ParentId, out _percentSum);
                            percentDictionary[(long) task.ParentId] = (_percentSum += task.PlannedHours * ((IProgressAuditedPlanTask) task).PercentComplete);
                            ((IProgressAuditedPlanTask) parent).PercentComplete = Math.Floor (_percentSum / parent.PlannedHours);
                            ((IProgressAuditedPlanTask) parent).IsCompleted = ((IProgressAuditedPlanTask) parent).PercentComplete == 100;
                        }
                    }
                }

                if (task is ISpannedPlanTask) {
                    output.EndDate = (DateTime) GeneralUtility.Max (((ISpannedPlanTask) task).FinishDate, output.EndDate);
                    output.StartDate = (DateTime) GeneralUtility.Min (((ISpannedPlanTask) task).StartDate, output.StartDate);

                    if (!task.IsMilestone && !task.IsHeaderTask) {
                        output.PlannedHours += task.PlannedHours;

                        if (task is IProgressAuditedPlanTask)
                            percentSum += task.PlannedHours * ((IProgressAuditedPlanTask) task).PercentComplete;
                    }
                }
            }

            output.PercentComplete = Math.Floor (percentSum / output.PlannedHours);
            return output;
        }

        public PlanDetail SortAndRecalculateProperties () {
            planTasks.Sort (new PlanTaskComparator<IBasicPlanTask> ());
            return RecalculateProperties ();
        }
    }
}
