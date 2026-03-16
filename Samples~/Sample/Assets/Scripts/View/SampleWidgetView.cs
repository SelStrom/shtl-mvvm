using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Shtl.Mvvm.Samples
{
    public sealed class SampleViewModel : AbstractViewModel
    {
        public readonly ReactiveValue<int> PerformedScore = new();
        public readonly ReactiveValue<float> Score = new();
        public readonly ReactiveList<ElementViewModel> Elements = new();

        public readonly ReactiveValue<Action> OnAddElementButtonClicked = new();
        public readonly ReactiveValue<Action> OnRemoveElementButtonClicked = new();
        public readonly ReactiveValue<Action> OnClearAllButtonClicked = new();

        public readonly SliderViewModel Slider = new();
        public readonly SliderViewModel ManualSlider = new();
    }

    public class SampleWidgetView : AbstractWidgetView<SampleViewModel>
    {
        [SerializeField] private TextMeshProUGUI _scoreTitle;
        [SerializeField] private TextMeshProUGUI _performedScoreTitle;
        [SerializeField] private List<ElementView> _elementList;
        [SerializeField] private ElementView _prefab;
        [SerializeField] private Transform _elementContainer;
        [SerializeField] private AutoSliderView _autoSliderView;
        [SerializeField] private ManualSliderView _manualSliderView;


        [SerializeField] private Button _addElementButton;
        [SerializeField] private Button _removeElementButton;
        [SerializeField] private Button _clearAll;

        protected override void OnConnected()
        {
            ViewModel.Score.Connect(score => _scoreTitle.text = score.ToString(CultureInfo.InvariantCulture));
            ViewModel.PerformedScore.Connect(value => _performedScoreTitle.text = value.ToString());

            Bind.From(_addElementButton).To(ViewModel.OnAddElementButtonClicked);
            Bind.From(_removeElementButton).To(ViewModel.OnRemoveElementButtonClicked);
            Bind.From(_clearAll).To(ViewModel.OnClearAllButtonClicked);

            Bind.From(ViewModel.Elements).To(_elementList, _prefab, _elementContainer);

            Bind.From(ViewModel.Slider).To(_autoSliderView);
            Bind.From(ViewModel.ManualSlider).To(_manualSliderView);
       }

        protected override void OnDisposed()
        {
            // Clean up dependencies
        }
    }
}
