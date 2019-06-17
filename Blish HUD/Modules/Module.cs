﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD.Content;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Blish_HUD.Modules {

    public enum ModuleRunState {
        /// <summary>
        /// The module is currently still working to complete its initial <see cref="Module.LoadAsync"/>.
        /// </summary>
        Loading,

        /// <summary>
        /// The module has completed loading and is enabled.
        /// </summary>
        Loaded,

        /// <summary>
        /// The module has been disabled and is currently unloading the resources it has.
        /// </summary>
        Unloading
    }

    public class ModuleRunStateChangedEventArgs : EventArgs {

        public ModuleRunState RunState { get; }

        public ModuleRunStateChangedEventArgs(ModuleRunState runState) {
            this.RunState = runState;
        }

    }

    public abstract class Module : IDisposable {

        #region Module Events

        public event EventHandler<ModuleRunStateChangedEventArgs> ModuleRunStateChanged;
        public event EventHandler<EventArgs>                      ModuleLoaded;

        public event EventHandler<UnobservedTaskExceptionEventArgs> ModuleException;

        internal void OnModuleRunStateChanged(ModuleRunStateChangedEventArgs e) {
            this.ModuleRunStateChanged?.Invoke(this, e);

            if (e.RunState == ModuleRunState.Loaded) {
                OnModuleLoaded(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Allows you to perform an action once your module has finished loading (once
        /// <see cref="LoadAsync"/> has completed).  You must call "base.OnModuleLoaded(e)" at the
        /// end for the <see cref="ExternalModule.ModuleLoaded"/> event to fire.
        /// </summary>
        protected virtual void OnModuleLoaded(EventArgs e) {
            ModuleLoaded?.Invoke(this, e);
        }

        protected void OnModuleException(UnobservedTaskExceptionEventArgs e) {
            ModuleException?.Invoke(this, e);
        }

        #endregion

        private readonly ModuleParameters _moduleParameters;

        private ModuleRunState _runState;
        internal ModuleRunState RunState {
            get => _runState;
            set {
                if (_runState == value) return;

                _runState = value;
                OnModuleRunStateChanged(new ModuleRunStateChangedEventArgs(_runState));
            }
        }

        public bool Loaded => _runState == ModuleRunState.Loaded;

        #region Manifest & Parameter Aliases

        // Manifest

        public string Name => _moduleParameters.Manifest.Name;

        public string Namespace => _moduleParameters.Manifest.Namespace;

        public SemVer.Version Version => _moduleParameters.Manifest.Version;

        // Service Managers

        protected SettingsManager SettingsManager => _moduleParameters.SettingsManager;

        protected ContentsManager ContentsManager => _moduleParameters.ContentsManager;

        protected DirectoriesManager DirectoriesManager => _moduleParameters.DirectoriesManager;

        protected Gw2ApiManager Gw2ApiManager => _moduleParameters.GW2ApiManager;

        #endregion

        private Task _loadTask;

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) {
            _moduleParameters = moduleParameters;
        }

        #region Module Method Interface

        public void DoInitialize() {
            DefineSettings(this.SettingsManager.ModuleSettings);

            Initialize();
        }

        public void DoLoad() {
            _loadTask = LoadAsync();
        }

        private void CheckForLoaded() {
            switch (_loadTask.Status) {
                case TaskStatus.Faulted:
                    var loadError = new UnobservedTaskExceptionEventArgs(_loadTask.Exception);
                    OnModuleException(loadError);
                    if (!loadError.Observed) {
                        GameService.Debug.WriteErrorLine($"Module '{this.Name} ({this.Namespace})' had an unhandled exception while loading:");
                        GameService.Debug.WriteErrorLine($"{loadError.Exception.ToString()}");
                    }
                    RunState = ModuleRunState.Loaded;
                    break;

                case TaskStatus.RanToCompletion:
                    RunState = ModuleRunState.Loaded;
                    GameService.Debug.WriteInfoLine($"Module '{this.Name} ({this.Namespace})' finished loading.");
                    break;

                case TaskStatus.Canceled:
                    GameService.Debug.WriteWarningLine($"Module '{this.Name} ({this.Namespace})' was cancelled before it could finish loading.");
                    break;

                case TaskStatus.WaitingForActivation:
                    break;

                default:
                    GameService.Debug.WriteWarningLine($"Unexpected module load result status '{_loadTask.Status.ToString()}'.");
                    break;
            }
        }

        public void DoUpdate(GameTime gameTime) {
            if (_runState == ModuleRunState.Loaded)
                Update(gameTime);
            else
                CheckForLoaded();
        }

        private void DoUnload() {
            this.RunState = ModuleRunState.Unloading;
            Unload();
        }

        #endregion

        #region Virtual Methods

        /// <summary>
        /// Allows your module to perform any initialization it needs before starting to run.
        /// Please note that Initialize is NOT asynchronous and will block Blish HUD's update
        /// and render loop, so be sure to not do anything here that takes too long.
        /// </summary>
        protected virtual void Initialize() { /* NOOP */ }

        /// <summary>
        /// Define the settings you would like to use in your module.  Settings are persistent
        /// between updates to both Blish HUD and your module.
        /// </summary>
        protected virtual void DefineSettings(SettingCollection settings) { /* NOOP */ }

        /// <summary>
        /// Load content and more here. This call is asynchronous, so it is a good time to
        /// run any long running steps for your module. Be careful when instancing
        /// <see cref="Blish_HUD.Entities.Entity"/> and <see cref="Blish_HUD.Controls.Control"/>.
        /// Setting their parent is not thread-safe and can cause the application to crash.
        /// You will want to queue them to add later while on the main thread or in a delegate queued
        /// with <see cref="Blish_HUD.DirectorService.QueueMainThreadUpdate(Action{GameTime})"/>.
        /// </summary>
        protected virtual async Task LoadAsync() { /* NOOP */ }

        /// <summary>
        /// Allows your module to run logic such as updating UI elements,
        /// checking for conditions, playing audio, calculating changes, etc.
        /// This method will block the primary Blish HUD loop, so any long
        /// running tasks should be executed on a separate thread to prevent
        /// slowing down the overlay.
        /// </summary>
        protected virtual void Update(GameTime gameTime) { /* NOOP */ }

        /// <summary>
        /// For a good module experience, your module should clean up ANY and ALL entities
        /// and controls that were created and added to either the World or SpriteScreen.
        /// Be sure to remove any tabs added to the Director window, CornerIcons, etc.
        /// </summary>
        protected virtual void Unload() { /* NOOP */ }

        #endregion

        #region IDispose

        protected void Dispose(bool disposing) {
            DoUnload();
        }

        /// <inheritdoc />
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        ~Module() {
            Dispose(false);
        }

        #endregion

    }

}
