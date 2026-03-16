using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shtl.Mvvm.Samples
{
    public class EntryScreen : MonoBehaviour
    {
        [SerializeField] private SampleWidgetView _widgetView;

        private SampleModel _model;
        private SampleViewModel _viewModel;

        private void Start()
        {
            CreateModel();
            CreateWidget();
        }

        private void Update()
        {
            // Simulate model changes
            var scoreValue = Mathf.Sin((DateTime.UtcNow.Ticks % 20000000) / 20000000f * Mathf.PI * 2);

            _model.Score.Value = scoreValue;
            _model.IntScore.Value = (int)(scoreValue * 100);

            foreach (var element in _model.Elements)
            {
                element.Score.Value = GetRandomValue();
            }

            _viewModel.Slider.Value.Value = (scoreValue + 1) * 0.5f;
        }

        private static int GetRandomValue() => UnityEngine.Random.Range(-1000, 1000);

        private void CreateWidget()
        {
            _viewModel = new SampleViewModel();
            _widgetView.Connect(_viewModel);
            new SampleWidget().Connect(_model, _viewModel);
        }

        private void CreateModel()
        {
            _model = new SampleModel();
            _model.Score = new ObservableValue<float>(GetRandomValue() * 1f);
            _model.IntScore = new ObservableValue<int>(GetRandomValue());

            _model.Elements = new List<ElementModel>
            {
                new() { Score = new ObservableValue<int>(GetRandomValue()) },
                new() { Score = new ObservableValue<int>(GetRandomValue()) },
                new() { Score = new ObservableValue<int>(GetRandomValue()) },
            };
        }
    }
}
