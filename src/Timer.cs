using Modding;
using UnityEngine;
using UnityEngine.UI;
using GlobalEnums;
using System;
using System.Reflection;

namespace HKTimer {
    public class Timer : MonoBehaviour {
        public TimeSpan time { get; set; } = TimeSpan.Zero;
        public bool timerActive { get; set; } = false;

        public GameObject timerCanvas { get; private set; }

        private Text frameDisplay;

        public void InitDisplay() {
            if (timerCanvas != null) {
                GameObject.DestroyImmediate(timerCanvas);
            }
            timerCanvas = CanvasUtil.CreateCanvas(UnityEngine.RenderMode.ScreenSpaceOverlay, 100);
            CanvasUtil.CreateFonts();
            frameDisplay = CanvasUtil.CreateTextPanel(
                timerCanvas,
                this.TimerText(),
                40,
                TextAnchor.MiddleRight,
                CreateTimerRectData(new Vector2(240, 40), new Vector2())
            ).GetComponent<Text>();
            UnityEngine.Object.DontDestroyOnLoad(timerCanvas);
        }

        public static CanvasUtil.RectData CreateTimerRectData(Vector2 size, Vector2 relPosition) {
            return new CanvasUtil.RectData(
                size,
                HKTimer.settings.timerPosition + relPosition,
                new Vector2(),
                new Vector2(),
                new Vector2(1, 0.5f)
            );
        }

        public void ShowDisplay(bool show) {
            this.timerCanvas.SetActive(show);
            if(show) GameObject.DontDestroyOnLoad(this.timerCanvas);
        }

        private string TimerText() {
            return string.Format(
                "{0}:{1:D2}.{2:D3}",
                Math.Floor(this.time.TotalMinutes),
                this.time.Seconds,
                this.time.Milliseconds
            );
        }

        public void OnDestroy() {
            GameObject.Destroy(timerCanvas);
        }

        public event Action OnTimerPause;
        public event Action OnTimerReset;

        public void Update() {
            var updateTimer = false;
            if (StringInputManager.GetKeyDown(HKTimer.settings.pause)) {
                timerActive ^= true;
                OnTimerPause?.Invoke();
            }
            if (StringInputManager.GetKeyDown(HKTimer.settings.reset)) {
                time = TimeSpan.Zero;
                timerActive = false;
                updateTimer = true;
                OnTimerReset?.Invoke();
            }
            if (timerActive && !TimerShouldBePaused()) {
                time += System.TimeSpan.FromSeconds(Time.unscaledDeltaTime);
                if (Time.unscaledDeltaTime > 0) updateTimer = true;
            }
            if (updateTimer) frameDisplay.text = this.TimerText();
        }


        // This uses the same disgusting logic as the autosplitter
        private bool lookForTeleporting;
        private GameState lastGameState = GameState.INACTIVE;

        private static FieldInfo cameraControlTeleporting = typeof(CameraController).GetField(
            "teleporting",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        private static FieldInfo gameManagerDirtyTileMap = typeof(GameManager).GetField(
            "tilemapDirty",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        private bool TimerShouldBePaused() {
            if (GameManager.instance == null) {
                // GameState is INACTIVE, so the teleporting code will run
                // teleporting defaults to false
                // (lookForTeleporting && (
                //    teleporting || (gameState != GameState.PLAYING && gameState != GameState.ENTERING_LEVEL)
                // ))
                lookForTeleporting = false;
                lastGameState = GameState.INACTIVE;
                return false;
            }

            var nextScene = GameManager.instance.nextSceneName;
            var sceneName = GameManager.instance.sceneName;
            var uiState = GameManager.instance.ui.uiState;
            var gameState = GameManager.instance.gameState;

            bool loadingMenu = (string.IsNullOrEmpty(nextScene) && sceneName != "Menu_Title") || (nextScene == "Menu_Title" && sceneName != "Menu_Title");
            if (gameState == GameState.PLAYING && lastGameState == GameState.MAIN_MENU) {
                lookForTeleporting = true;
            }
            bool teleporting = (bool)cameraControlTeleporting.GetValue(GameManager.instance.cameraCtrl);
            if (lookForTeleporting && (teleporting || (gameState != GameState.PLAYING && gameState != GameState.ENTERING_LEVEL))) {
                lookForTeleporting = false;
            }

            var shouldPause =
                (
                    gameState == GameState.PLAYING
                    && teleporting
                    && !(
                        GameManager.instance.hero_ctrl == null ? false :
                            GameManager.instance.hero_ctrl.cState.hazardRespawning
                    )
                )
                || lookForTeleporting
                || ((gameState == GameState.PLAYING || gameState == GameState.ENTERING_LEVEL) && uiState != UIState.PLAYING)
                || (gameState != GameState.PLAYING && !GameManager.instance.inputHandler.acceptingInput)
                || gameState == GameState.EXITING_LEVEL
                || gameState == GameState.LOADING
                || (
                    GameManager.instance.hero_ctrl == null ? false :
                    GameManager.instance.hero_ctrl.transitionState == HeroTransitionState.WAITING_TO_ENTER_LEVEL
                )
                || (
                    uiState != UIState.PLAYING
                    && (uiState != UIState.PAUSED || loadingMenu)
                    && (!string.IsNullOrEmpty(nextScene) || sceneName == "_test_charms" || loadingMenu)
                    && nextScene != sceneName
                )
                || (bool)gameManagerDirtyTileMap.GetValue(GameManager.instance);

            lastGameState = gameState;

            return shouldPause;
        }
    }
}