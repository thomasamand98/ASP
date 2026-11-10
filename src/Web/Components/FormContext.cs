namespace Horizon.Web.Components;

// Initial null en création
public sealed record FormContext<TDetail, TDto>(TDetail? Initial, Func<TDto, Task> OnSubmit, Func<Task> OnCancel);
