using System.Collections.Generic;

namespace Shtl.Mvvm
{
    public abstract class AbstractViewModel : IReactiveValue
    {
        private readonly List<IReactiveValue> _viewModelFields = new();

        protected AbstractViewModel()
        {
            foreach (var fieldInfo in GetType().GetFields())
            {
                if (fieldInfo.GetValue(this) is IReactiveValue variable)
                {
                    _viewModelFields.Add(variable);
                }
            }
        }

        public void Dispose()
        {
            foreach (var variable in _viewModelFields)
            {
                variable.Dispose();
            }
        }

        public void Unbind()
        {
            foreach (var variable in _viewModelFields)
            {
                variable.Unbind();
            }
        }
    }
}
