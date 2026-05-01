using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace Shtl.Mvvm.Editor
{
    public class ViewModelDrawer
    {
        public bool ShowUnsupportedFields { get; set; }
        public Action OnStructureChanged { get; set; }

        private readonly Dictionary<object, bool> _objectToFoldoutStatus = new();
        private readonly bool _isEditable;

        private readonly List<Action> _valueUpdaters = new();
        private readonly List<Func<bool>> _structureChecks = new();

        public ViewModelDrawer(bool isEditable = true)
        {
            _isEditable = isEditable;
        }

        public VisualElement BuildViewModelElement(Type viewModelType, object viewModel)
        {
            _valueUpdaters.Clear();
            _structureChecks.Clear();

            var container = new VisualElement();
            AddViewModelFields(container, viewModelType, viewModel);
            return container;
        }

        /// <returns>true if a structural rebuild is needed</returns>
        public bool UpdateValues()
        {
            foreach (var check in _structureChecks)
            {
                if (check())
                {
                    return true;
                }
            }

            foreach (var updater in _valueUpdaters)
            {
                updater();
            }

            return false;
        }

        private void AddViewModelFields(VisualElement container, Type viewModelType, object viewModel)
        {
            var fields = viewModelType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(viewModel);
                var element = BuildParameter(field, fieldValue);
                if (element != null)
                {
                    container.Add(element);
                }
            }
        }

        private VisualElement BuildParameter(FieldInfo field, object fieldValue)
        {
            if (typeof(AbstractViewModel).IsAssignableFrom(field.FieldType))
            {
                _objectToFoldoutStatus.TryGetValue(fieldValue, out var isExpanded);
                var foldout = new Foldout
                {
                    text = FormatLabel(field.Name, field.FieldType),
                    value = isExpanded
                };
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target == foldout)
                    {
                        _objectToFoldoutStatus[fieldValue] = evt.newValue;
                    }
                });

                AddViewModelFields(foldout, field.FieldType, fieldValue);
                return foldout;
            }

            if (IsGenericTypeOf(field.FieldType, typeof(ReactiveList<>)))
            {
                return BuildEnumerable(field, fieldValue as IEnumerable, ((IReactiveListCount)fieldValue).Count);
            }

            if (IsGenericTypeOf(field.FieldType, typeof(ReactiveVirtualList<>)))
            {
                return BuildReactiveVirtualList(field, fieldValue);
            }

            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var collection = fieldValue as ICollection;
                return BuildEnumerable(field, collection, collection!.Count);
            }

            if (IsGenericTypeOf(field.FieldType, typeof(ReactiveValue<>)))
            {
                var parameterType = field.FieldType.GetGenericArguments().First();
                return BuildReactiveValue(field.Name, parameterType, fieldValue);
            }

            if (!ShowUnsupportedFields)
            {
                return null;
            }

            return new Label($"{field.Name}: Type '{field.FieldType.Name}' is not supported");
        }

        private VisualElement BuildReactiveVirtualList(FieldInfo field, object fieldValue)
        {
            var itemsField = field.FieldType.GetField("Items");
            var items = itemsField!.GetValue(fieldValue);
            var count = ((IReactiveListCount)items).Count;

            _objectToFoldoutStatus.TryGetValue(fieldValue, out var isExpanded);
            var foldout = new Foldout
            {
                text = FormatLabel($"{field.Name} [{count}]", field.FieldType),
                value = isExpanded
            };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout)
                {
                    _objectToFoldoutStatus[fieldValue] = evt.newValue;
                }
            });

            // Items list — reuse existing enumerable rendering
            var itemsFoldoutElement = BuildEnumerable(itemsField, items as IEnumerable, count);
            foldout.Add(itemsFoldoutElement);

            // Scalar virtual-scroll state fields
            var scrollPositionField = field.FieldType.GetField("ScrollPosition");
            var firstVisibleIndexField = field.FieldType.GetField("FirstVisibleIndex");
            var visibleCountField = field.FieldType.GetField("VisibleCount");

            foldout.Add(BuildReactiveValue(
                "ScrollPosition",
                typeof(float),
                scrollPositionField!.GetValue(fieldValue)));

            foldout.Add(BuildReactiveValue(
                "FirstVisibleIndex",
                typeof(int),
                firstVisibleIndexField!.GetValue(fieldValue)));

            foldout.Add(BuildReactiveValue(
                "VisibleCount",
                typeof(int),
                visibleCountField!.GetValue(fieldValue)));

            return foldout;
        }

        private VisualElement BuildEnumerable(FieldInfo field, IEnumerable elements, int size)
        {
            _objectToFoldoutStatus.TryGetValue(elements, out var isExpanded);
            var foldout = new Foldout
            {
                text = FormatLabel($"{field.Name} [{size}]", field.FieldType),
                value = isExpanded
            };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout)
                {
                    _objectToFoldoutStatus[elements] = evt.newValue;
                }
            });

            var elementType = field.FieldType.GetGenericArguments().First();

            var capturedSize = size;
            if (elements is IReactiveListCount reactiveList)
            {
                _structureChecks.Add(() => reactiveList.Count != capturedSize);
            }
            else if (elements is ICollection collection)
            {
                _structureChecks.Add(() => collection.Count != capturedSize);
            }

            var i = 0;
            foreach (var element in elements)
            {
                var index = i;
                var capturedElement = element;

                _objectToFoldoutStatus.TryGetValue(capturedElement, out var isElementExpanded);
                var elementFoldout = new Foldout
                {
                    text = $"[{i}]: {elementType.Name}",
                    value = isElementExpanded
                };
                elementFoldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.target == elementFoldout)
                    {
                        _objectToFoldoutStatus[capturedElement] = evt.newValue;
                    }
                });

                BuildListElement(elementFoldout, $"[{index}]", elementType, capturedElement);

                if (_isEditable)
                {
                    elementFoldout.Add(new Button(() =>
                    {
                        field.FieldType.GetMethod("RemoveAt")!.Invoke(elements, new object[] { index });
                        OnStructureChanged?.Invoke();
                    }) { text = "Delete element" });
                }

                foldout.Add(elementFoldout);
                i++;
            }

            if (_isEditable)
            {
                foldout.Add(new Button(() =>
                {
                    var newElement = Activator.CreateInstance(elementType);
                    field.FieldType.GetMethod("Add")!.Invoke(elements, new[] { newElement });
                    OnStructureChanged?.Invoke();
                }) { text = "Add new element" });
            }

            return foldout;
        }

        private void BuildListElement(VisualElement container, string name, Type elementType, object element)
        {
            if (typeof(IReactiveValue).IsAssignableFrom(elementType))
            {
                AddViewModelFields(container, elementType, element);
            }
            else if (ShowUnsupportedFields)
            {
                container.Add(new Label($"{name}: Type '{elementType.Name}' is not supported")
                {
                    style = { color = Color.grey }
                });
            }
        }

        private VisualElement BuildReactiveValue(string fieldName, Type fieldType, object parameter)
        {
            var propertyInfo = parameter.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)!;
            var initialValue = propertyInfo.GetValue(parameter);

            Func<object> getValue = () => propertyInfo.GetValue(parameter);
            Action<object> setValue = newValue =>
            {
                if (_isEditable)
                {
                    propertyInfo.SetValue(parameter, newValue);
                }
            };

            return BuildField(fieldName, fieldType, initialValue, getValue, setValue);
        }

        private VisualElement BuildField(string fieldName, Type fieldType, object initialValue,
            Func<object> getValue, Action<object> setValue)
        {
            if (fieldType == typeof(int))
            {
                var field = new IntegerField(fieldName) { value = (int)initialValue };
                field.RegisterValueChangedCallback(evt => setValue(evt.newValue));
                _valueUpdaters.Add(() => field.SetValueWithoutNotify((int)getValue()));
                return field;
            }

            if (fieldType == typeof(float))
            {
                var field = new FloatField(fieldName) { value = (float)initialValue };
                field.RegisterValueChangedCallback(evt => setValue(evt.newValue));
                _valueUpdaters.Add(() => field.SetValueWithoutNotify((float)getValue()));
                return field;
            }

            if (fieldType == typeof(long))
            {
                var field = new LongField(fieldName) { value = (long)initialValue };
                field.RegisterValueChangedCallback(evt => setValue(evt.newValue));
                _valueUpdaters.Add(() => field.SetValueWithoutNotify((long)getValue()));
                return field;
            }

            if (fieldType == typeof(string))
            {
                var field = new TextField(fieldName) { value = (string)initialValue ?? "" };
                field.RegisterValueChangedCallback(evt => setValue(evt.newValue));
                _valueUpdaters.Add(() => field.SetValueWithoutNotify((string)getValue() ?? ""));
                return field;
            }

            if (fieldType == typeof(bool))
            {
                var field = new Toggle(fieldName) { value = (bool)initialValue };
                field.RegisterValueChangedCallback(evt => setValue(evt.newValue));
                _valueUpdaters.Add(() => field.SetValueWithoutNotify((bool)getValue()));
                return field;
            }

            if (fieldType.IsEnum)
            {
                var field = new EnumField(fieldName, (Enum)initialValue);
                field.RegisterValueChangedCallback(evt => setValue(evt.newValue));
                _valueUpdaters.Add(() => field.SetValueWithoutNotify((Enum)getValue()));
                return field;
            }

            if (fieldType.Name.AsSpan().StartsWith(nameof(ValueTuple)))
            {
                return BuildValueTuple(fieldType, fieldName, getValue);
            }

            if (typeof(Object).IsAssignableFrom(fieldType))
            {
                var field = new ObjectField(fieldName)
                {
                    objectType = fieldType,
                    allowSceneObjects = true,
                    value = (Object)initialValue
                };
                field.RegisterValueChangedCallback(evt => setValue(evt.newValue));
                _valueUpdaters.Add(() => field.SetValueWithoutNotify((Object)getValue()));
                return field;
            }

            if (!ShowUnsupportedFields)
            {
                return null;
            }

            return new Label($"{fieldName}: Type '{fieldType.Name}' is not supported")
            {
                style = { color = new Color(1f, 1f, 0f) }
            };
        }

        private VisualElement BuildValueTuple(Type tupleType, string fieldName, Func<object> getTuple)
        {
            var foldout = new Foldout
            {
                text = FormatLabel(fieldName, tupleType),
                value = true
            };

            var genericArguments = tupleType.GetGenericArguments();
            var itemIndex = 1;
            foreach (var genericArgument in genericArguments)
            {
                var name = $"Item{itemIndex}";
                var fieldInfo = tupleType.GetField(name)!;
                var capturedFieldInfo = fieldInfo;

                Func<object> getItem = () => capturedFieldInfo.GetValue(getTuple());
                var initialValue = capturedFieldInfo.GetValue(getTuple());

                var element = BuildField(name, genericArgument, initialValue, getItem, _ => { });
                if (element != null)
                {
                    foldout.Add(element);
                }
                itemIndex++;
            }

            //TODO make editable https://app.asana.com/1/656176460444/project/1201440448827668/task/1209698428961132?focus=true
            return foldout;
        }

        private static string FormatLabel(string name, Type type) => $"{name}  ({type})";

        private static bool IsGenericTypeOf(Type type, Type genericType)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericType)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }
    }
}
