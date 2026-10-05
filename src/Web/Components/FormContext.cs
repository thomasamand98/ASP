namespace Horizon.Web.Components;

// Ce que CrudPage passe au formulaire d'un écran : l'enregistrement à
// modifier (null en création), l'enregistrement, l'abandon.
public sealed record FormContext<TDetail, TDto>(TDetail? Initial, Func<TDto, Task> OnSubmit, Func<Task> OnCancel);
