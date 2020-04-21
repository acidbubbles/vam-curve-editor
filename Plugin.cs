using CurveEditor.UI;
using SimpleJSON;
using System;
using System.Linq;
using UnityEngine;

namespace CurveEditor
{
    public partial class Plugin : MVRScript
    {
        public static readonly string PluginName = "Curve Editor Demo";
        public static readonly string PluginAuthor = "Yoooi";

        private UICurveEditor _curveEditor;
        private Animation _animation;
        private JSONStorableAnimationCurve _curve1JSON, _curve2JSON;

        public override void Init()
        {
            try
            {
                if (containingAtom != null)
                    _animation = containingAtom.GetComponent<Animation>() ?? containingAtom.gameObject.AddComponent<Animation>();

                _curve1JSON = new JSONStorableAnimationCurve("Curve 1", CurveUpdated);
                _curve1JSON.val = AnimationCurve.Linear(0, 0, 2, 0);
                _curve2JSON = new JSONStorableAnimationCurve("Curve 2", CurveUpdated);
                _curve2JSON.val = AnimationCurve.EaseInOut(0, 0, 2, 0);

                CreateUI();
            }
            catch (Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        public void CreateUI()
        {
            pluginLabelJSON.val = PluginName;

            var container = CreateSpacer();
            container.height = 300;

            var curveEditorButtons = Enumerable.Range(0, 4)
                .Select(_ => UnityEngine.Object.Instantiate(manager.configurableButtonPrefab))
                .Select(t => t.GetComponent<UIDynamicButton>())
                .ToList();

            foreach (var b in curveEditorButtons)
            {
                b.buttonText.fontSize = 18;
                b.buttonColor = Color.white;
            }

            curveEditorButtons[0].label = "Mode";
            curveEditorButtons[1].label = "In Mode";
            curveEditorButtons[2].label = "Out Mode";
            curveEditorButtons[3].label = "Linear";

            curveEditorButtons[0].button.onClick.AddListener(() => _curveEditor.ToggleHandleMode());
            curveEditorButtons[1].button.onClick.AddListener(() => _curveEditor.ToggleInHandleMode());
            curveEditorButtons[2].button.onClick.AddListener(() => _curveEditor.ToggleOutHandleMode());
            curveEditorButtons[3].button.onClick.AddListener(() => _curveEditor.SetLinear());

            _curveEditor = new UICurveEditor(container, 520, container.height, buttons: curveEditorButtons);
            _curveEditor.AddCurve(_curve1JSON, UICurveLineColors.CreateFrom(new Color(0.388f, 0.698f, 0.890f)));
            _curveEditor.SetValueBounds(_curve1JSON, Vector2.zero, new Vector2(2, 1f));
            _curveEditor.AddCurve(_curve2JSON, UICurveLineColors.CreateFrom(new Color(0.890f, 0.388f, 0.398f)));
            _curveEditor.SetValueBounds(_curve2JSON, Vector2.zero, new Vector2(2, 1f));

            var resetButton = CreateButton("Reset");
            var playButton = CreateButton("Play");
            var stopButton = CreateButton("Stop");
            var fitButton = CreateButton("Fit View");

            resetButton.button.onClick.AddListener(() =>
            {
                _curve1JSON.SetValToDefault();
                _curveEditor.UpdateCurve(_curve1JSON);
                _curve2JSON.SetValToDefault();
                _curveEditor.UpdateCurve(_curve2JSON);
            });
            playButton.button.onClick.AddListener(() =>
            {
                _animation.Play("CurveEditorDemo");
            });
            stopButton.button.onClick.AddListener(() =>
            {
                _animation.Stop();
            });
            fitButton.button.onClick.AddListener(() =>
            {
                _curveEditor.SetViewToFit();
            });

            var readOnlyStorable = new JSONStorableBool("ReadOnly", false);
            var readOnlyToggle = CreateToggle(readOnlyStorable);
            readOnlyStorable.setCallbackFunction = v => _curveEditor.readOnly = v;

            var showScrubberStorable = new JSONStorableBool("Show Scrubbers", true);
            var showScrubberToggle = CreateToggle(showScrubberStorable);
            showScrubberStorable.setCallbackFunction = v => _curveEditor.showScrubbers = v;

            var scrubberSliderStorable = new JSONStorableFloat("Scrubber time", 0, 0, 2);
            var scrubberSlider = CreateSlider(scrubberSliderStorable);

            scrubberSliderStorable.setCallbackFunction = v =>
            {
                var state = _animation["CurveEditorDemo"];
                if (state != null)
                {
                    state.time = v;
                }
                _curveEditor.SetScrubber(_curve1JSON, v);
                _curveEditor.SetScrubber(_curve2JSON, 2 - v);
            };

            var timeScaleSliderStorable = new JSONStorableFloat("Time Scale", 1, 0.5f, 2);
            var valueScaleSliderStorable = new JSONStorableFloat("Value Scale", 1, 0.5f, 2);

            var timeScaleSlider = CreateSlider(timeScaleSliderStorable);

            timeScaleSliderStorable.setCallbackFunction = v =>
            {
                _curveEditor.SetValueBounds(_curve1JSON, Vector2.zero, new Vector2(v, valueScaleSliderStorable.val));
                _curveEditor.SetValueBounds(_curve2JSON, Vector2.zero, new Vector2(v, valueScaleSliderStorable.val));
            };

            var valueScaleSlider = CreateSlider(valueScaleSliderStorable);

            valueScaleSliderStorable.setCallbackFunction = v =>
            {
                _curveEditor.SetValueBounds(_curve1JSON, Vector2.zero, new Vector2(timeScaleSliderStorable.val, v));
                _curveEditor.SetValueBounds(_curve2JSON, Vector2.zero, new Vector2(timeScaleSliderStorable.val, v));
            };
        }

        protected void Update()
        {
            if (_animation == null || _curve1JSON == null || _curveEditor == null) return;
            if (_animation.isPlaying)
            {
                var state = _animation["CurveEditorDemo"];
                if (state != null)
                    _curveEditor.SetScrubberPosition(state.time % _curve1JSON.val[_curve1JSON.val.length - 1].time);
            }
        }

        protected void OnDestroy()
        {
            // NOTE: We don't destroy the animation because reloading plugins will create before the previous one is destroyed.
            RemoveSpacer(_curveEditor.container);
        }

        private void CurveUpdated(AnimationCurve curve)
        {
            if (_animation == null)
                return;

            var playing = _animation.isPlaying;
            var time = _animation["CurveEditorDemo"]?.time ?? 0;
            var clip = new AnimationClip
            {
                legacy = true,
                wrapMode = WrapMode.Loop
            };
            clip.SetCurve("", typeof(Transform), "localPosition.x", curve);
            _animation.AddClip(clip, "CurveEditorDemo");
            _animation["CurveEditorDemo"].time = time;
            _animation.Play("CurveEditorDemo");
            if (!playing)
                _animation.Stop("CurveEditorDemo");
        }

        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            var jc = base.GetJSON(includePhysical, includeAppearance, forceStore);
            if (_curve1JSON.StoreJSON(jc, includePhysical, includeAppearance, forceStore)) needsStore = true;
            return jc;
        }

        public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
        {
            base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);

            _curve1JSON.RestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);
            _curveEditor.UpdateCurve(_curve1JSON);
        }

        public override void LateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true)
            => RestoreFromJSON(jc, restorePhysical, restoreAppearance, null, setMissingToDefault);
    }
}
