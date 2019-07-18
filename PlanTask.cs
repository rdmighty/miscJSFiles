using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace CounterpartProjects.PlanningTool {
    // These are must haves
    public interface IBasicPlanTask {
        long Id { get; set; }
        int Order { get; set; }
        string TaskId { get; set; }
        long? ParentId { get; set; }
        long TaskNumber { get; set; }
        int Indentation { get; set; }
        bool IsExpanded { get; set; }
        bool IsMilestone { get; set; }
        bool IsHeaderTask { get; set; }
        decimal PlannedHours { get; set; }
    }

    // These are optional
    public interface ISpannedPlanTask {
        DateTime? StartDate { get; set; }
        DateTime? FinishDate { get; set; }
    }

    public interface IActualAuditedPlanTask {
        decimal ActualHours { get; set; }
        DateTime? ActualStartDate { get; set; }
        DateTime? ActualFinishDate { get; set; }
    }

    public interface IProgressAuditedPlanTask {
        decimal PercentComplete { get; set; }
        bool IsCompleted { get; set; }
    }

    public interface IPlanTaskWithCost {
        decimal Cost { get; set; }
    }
}
