using JDFixer.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zenject;


namespace JDFixer.Managers
{
    internal class JDFixerUIManager : IInitializable, IDisposable
    {
        private static StandardLevelDetailViewController levelDetail;
        private static MissionSelectionMapViewController missionSelection;
        private static BeatmapLevelsModel levelsModel;
        private static MainMenuViewController mainMenu;

        private readonly List<IBeatmapInfoUpdater> beatmapInfoUpdaters;
        private readonly List<IRefreshable> refreshables;


        [Inject]
        private JDFixerUIManager(
            StandardLevelDetailViewController standardLevelDetailViewController,
            MissionSelectionMapViewController missionSelectionMapViewController,
            BeatmapLevelsModel beatmapLevelsModel,
            MainMenuViewController mainMenuViewController,
            List<IBeatmapInfoUpdater> iBeatmapInfoUpdaters,
            List<IRefreshable> refreshableViews)
        {
            //Plugin.Log.Debug("JDFixerUIManager()");

            levelDetail = standardLevelDetailViewController;
            missionSelection = missionSelectionMapViewController;
            levelsModel = beatmapLevelsModel;
            mainMenu = mainMenuViewController;

            beatmapInfoUpdaters = iBeatmapInfoUpdaters;
            refreshables = refreshableViews;
        }


        public void Initialize()
        {
            //Plugin.Log.Debug("Initialize()");

            levelDetail.didChangeDifficultyBeatmapEvent += LevelDetail_didChangeDifficultyBeatmapEvent;
            levelDetail.didChangeContentEvent += LevelDetail_didChangeContentEvent;

            if (Plugin.CheckForCustomCampaigns())
            {
                missionSelection.didSelectMissionLevelEvent += MissionSelection_didSelectMissionLevelEvent_CC;
            }
            else
            {
                missionSelection.didSelectMissionLevelEvent += MissionSelection_didSelectMissionLevelEvent_Base;
            }

            mainMenu.didDeactivateEvent += MainMenu_didDeactivateEvent; ;
        }


        public void Dispose()
        {
            //Plugin.Log.Debug("Dispose()");

            levelDetail.didChangeDifficultyBeatmapEvent -= LevelDetail_didChangeDifficultyBeatmapEvent;
            levelDetail.didChangeContentEvent -= LevelDetail_didChangeContentEvent;

            missionSelection.didSelectMissionLevelEvent -= MissionSelection_didSelectMissionLevelEvent_CC;
            missionSelection.didSelectMissionLevelEvent -= MissionSelection_didSelectMissionLevelEvent_Base;

            mainMenu.didDeactivateEvent -= MainMenu_didDeactivateEvent;
        }


        private void LevelDetail_didChangeDifficultyBeatmapEvent(StandardLevelDetailViewController arg1)
        {
            //Plugin.Log.Debug("LevelDetail_didChangeDifficultyBeatmapEvent()");

            if (arg1 != null)
            {
                BeatmapUpdated(arg1.beatmapKey, arg1.beatmapLevel);
            }
        }


        private void LevelDetail_didChangeContentEvent(StandardLevelDetailViewController arg1, StandardLevelDetailViewController.ContentType arg2)
        {
            //Plugin.Log.Debug("LevelDetail_didChangeContentEvent()");          
            
            if (arg1 != null && arg1.beatmapLevel != null)//selectedDifficultyBeatmap != null)
            {
                //Plugin.Log.Debug("NJS: " + arg1.selectedDifficultyBeatmap.noteJumpMovementSpeed);
                //Plugin.Log.Debug("Offset: " + arg1.selectedDifficultyBeatmap.noteJumpStartBeatOffset);

                BeatmapUpdated(arg1.beatmapKey, arg1.beatmapLevel); //selectedDifficultyBeatmap);
            }
        }


        private void MissionSelection_didSelectMissionLevelEvent_CC(MissionSelectionMapViewController controller, MissionNode node)
        {
            // Both missionData and missionData.beatmapCharacteristic must be checked: for a map that is
            // not downloaded, missionID and beatmapDifficulty are still correct but
            // beatmapCharacteristic is null, and dereferencing either crashes CustomCampaigns.
            if (node?.missionData?.beatmapCharacteristic == null)
            {
                // Map not downloaded.
                BeatmapUpdated(new BeatmapKey(), null);
                return;
            }

            Plugin.Log.Debug("In CC, MissionNode exists");

            // CustomMissionDataSO.beatmapLevel exists only in CustomCampaigns, so it is resolved
            // reflectively to avoid a compile-time dependency on the CC assembly. If CC renames the
            // property the map simply shows no jump distance, which is better than a hard failure.
            var missionData = node.missionData;
            var beatmapLevel = CustomCampaignsBeatmapLevel?.Invoke(missionData) as BeatmapLevel;

            if (beatmapLevel != null)
            {
                BeatmapUpdated(missionData.beatmapKey, beatmapLevel);
            }
        }


        private void MissionSelection_didSelectMissionLevelEvent_Base(MissionSelectionMapViewController controller, MissionNode node)
        {
            // Base campaign. missionData is null for nodes with no mission assigned, so it needs the
            // same guard the CustomCampaigns path has.
            if (node?.missionData == null)
            {
                return;
            }

            var beatmapKey = node.missionData.beatmapKey;
            BeatmapUpdated(beatmapKey, levelsModel?.GetBeatmapLevel(beatmapKey.levelId));
        }

        /// <summary>
        /// Accessor for <c>CustomMissionDataSO.beatmapLevel</c>, resolved once. Null when
        /// CustomCampaigns is not installed, in which case the CC path degrades to showing nothing
        /// rather than throwing on every map selection.
        /// </summary>
        private static readonly Func<object, object> CustomCampaignsBeatmapLevel = CreateCustomCampaignsAccessor();

        private static Func<object, object> CreateCustomCampaignsAccessor()
        {
            try
            {
                Type customMissionData = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType("CustomCampaigns.CustomMissionDataSO", throwOnError: false))
                    .FirstOrDefault(t => t != null);

                if (customMissionData == null)
                {
                    Plugin.Log.Debug("CustomCampaigns not present; CC jump distance display disabled");
                    return null;
                }

                PropertyInfo property = customMissionData.GetProperty("beatmapLevel", BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    Plugin.Log.Warn("CustomMissionDataSO.beatmapLevel not found; CC jump distance display disabled");
                    return null;
                }

                return missionData => property.GetValue(missionData);
            }
            catch (Exception e)
            {
                Plugin.Log.Warn($"Failed to resolve CustomCampaigns beatmapLevel: {e.Message}");
                return null;
            }
        }


        private void MainMenu_didDeactivateEvent(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            foreach (var refreshable in refreshables)
            {
                refreshable.Refresh();
            }
        }


        private void BeatmapUpdated(BeatmapKey beatmapKey, BeatmapLevel beatmapLevel)
        {
            // Built once and shared: the object is derived state that every subscriber reads, and
            // constructing it per subscriber meant each view could observe a different instance.
            var info = new BeatmapInfo(beatmapKey, beatmapLevel);

            foreach (var beatmapInfoUpdater in beatmapInfoUpdaters)
            {
                beatmapInfoUpdater.BeatmapInfoUpdated(info);
            }
        }
    }
}