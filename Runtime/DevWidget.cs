using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shtl.Mvvm
{
    [DefaultExecutionOrder(-1)]
    [ExecuteInEditMode, DisallowMultipleComponent]
    public class DevWidget : MonoBehaviour
    {
        [SerializeField] private GameObject _gui;
        [SerializeField] private GameObject _uiPrefab;

#if UNITY_EDITOR
        public MonoBehaviour WidgetViewComponent { get; private set; }
        public Type ViewModelType { get; private set; }
        public AbstractViewModel ViewModel { get; private set; }

        [UsedImplicitly, InitializeOnLoadMethod]
        private static void RunObserver()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange _)
        {
            if (!SceneManager.GetActiveScene().isDirty)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var widgets = FindObjectsByType<DevWidget>(FindObjectsSortMode.None);
#else
            var widgets = FindObjectsOfType<DevWidget>();
#endif
            foreach (var widget in widgets)
            {
                widget.ClosePrefab();
            }
        }

        public void UpdateViewModel(object viewModel)
        {
            ViewModel = (AbstractViewModel)viewModel;
            InjectViewModel();
        }

        public void OpenPrefab()
        {
            CleanUp();
            UpdateContent();
        }

        public void ClosePrefab()
        {
            CleanUp();
        }

        private void UpdateContent()
        {
            WidgetViewComponent = Instantiate(_uiPrefab, _gui.transform).GetComponents<MonoBehaviour>()
                .FirstOrDefault(x => IsSubclassOfAbstractWidgetView(x.GetType().BaseType));

            var widgetType = WidgetViewComponent!.GetType();
            ViewModelType = widgetType.BaseType?.GetGenericArguments().FirstOrDefault();

            ViewModel = (AbstractViewModel)Activator.CreateInstance(ViewModelType!);
            InjectViewModel();
        }

        private void InjectViewModel()
        {
            var genericType = typeof(AbstractWidgetView<>).MakeGenericType(ViewModelType);
            var injectMethod = genericType.GetMethod("Connect");
            injectMethod!.Invoke(WidgetViewComponent, new object[] { ViewModel });
        }

        private void CleanUp()
        {
            if (WidgetViewComponent != null)
            {
                foreach (Transform child in _gui.transform)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            ViewModel = null;
            WidgetViewComponent = null;
        }

        private static bool IsSubclassOfAbstractWidgetView(Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AbstractWidgetView<>))
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }
#endif
    }
}
