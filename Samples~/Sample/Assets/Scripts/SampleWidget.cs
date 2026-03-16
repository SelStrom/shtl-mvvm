using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shtl.Mvvm;
using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    [Serializable]
    public class SampleWidget
    {
        private SampleViewModel _viewModel;
        private SampleModel _model;

        private IEventBindingContext _bindingContext;
        protected IEventBindingContext Bind => _bindingContext ??= new EventBindingContext();
        private static int _targetPerformedScope;

        public void Connect(SampleModel sampleModel, SampleViewModel viewModel)
        {
            _model = sampleModel;
            _viewModel = viewModel;

            ConnectModelWithVm();
            FillViewModel();
        }

        private void ConnectModelWithVm()
        {
            SetupButtons();
            _viewModel.ManualSlider.OnValueChanged.Value += x =>
            {
                _viewModel.ManualSlider.Value.Value = x;
            };
        }

        private void SetupButtons()
        {
            // Bind model value to view model
            Bind.From(_model.Score).To(_viewModel.Score);

            // Bind model value to view model via transform function
            Bind.From(_model.IntScore).To(_viewModel.PerformedScore, PerformScore);

            // Bind button to callback
            _viewModel.OnAddElementButtonClicked.Value = _model.AddNewElement;
            _viewModel.OnRemoveElementButtonClicked.Value = _model.RemoveRandomElement;
            _viewModel.OnClearAllButtonClicked.Value = OnClearAll;

            // Fallback for logic not supported by the binding system
            _model.OnElementAdded += AddElementAdded;
            _model.OnElementRemoved += OnElementRemoved;
        }

        private void ActivateGreenPage()
        {
            Debug.Log("GreenPage activated");
        }

        private void ActivateBluePage()
        {
            Debug.Log("BluePage activated");
        }

        private void ActivateRedPage()
        {
            Debug.Log(" RedPage activated");
        }

        private void FillViewModel()
        {
            foreach (var elementModel in _model.Elements)
            {
                AddElementAdded(elementModel);
            }
        }

        private void AddElementAdded(ElementModel element)
        {
            var elementVm = new ElementViewModel
            {
                Title = { Value = string.Empty },
                Score = { Value = 100500 },
            };
            elementVm.OnButtonClicked.Value = () => _ = DoAnimateAsync(elementVm);

            _viewModel.Elements.Add(elementVm);

            Bind.From(element.Score).To(elementVm.Score);
        }

        private void OnElementRemoved(int index) => _viewModel.Elements.RemoveAt(index);

        private async Task DoAnimateAsync(ElementViewModel elementVm)
        {
            Debug.Log("Animation animation started");
            foreach (var element in _viewModel.Elements)
            {
                var isAnimating = element == elementVm;
                element.LockForAnimation.Value = !isAnimating;
                element.ButtonLabel.Value = isAnimating ? "Animating" : "Input locked";
            }

            await elementVm.WaitForAnimation.StartAsync();

            Debug.Log("Animation completed");
            foreach (var element in _viewModel.Elements)
            {
                element.LockForAnimation.Value = false;
                element.ButtonLabel.Value = "No animation";
            }
        }

        private static void PerformScore(int sourceValue, ReactiveValue<int> context) => context.Value = sourceValue;

        private void OnClearAll()
        {
            // Direct model clearing without callbacks (edge case)
            _model.ClearAll();
            _viewModel.Elements.Clear();
        }
    }
}
