﻿using Newtonsoft.Json;
using SAIN.Attributes;

namespace SAIN.Preset.Personalities
{
    public class PersonalitySettingsClass : SettingsGroupBase<PersonalitySettingsClass>
    {
        [JsonConstructor]
        public PersonalitySettingsClass()
        {
        }

        public PersonalitySettingsClass(EPersonality personality)
        {
            Name = personality.ToString();
            Description = PersonalityDescriptionsClass.PersonalityDescriptions[personality];
        }

        [Hidden]
        public string Name;
        [Hidden]
        public string Description;
        public PersonalityAssignmentSettings Assignment = new PersonalityAssignmentSettings();
        public PersonalityBehaviorSettings Behavior = new PersonalityBehaviorSettings();
        [Hidden]
        public PersonalityStatModifierSettings StatModifiers = new PersonalityStatModifierSettings();

        public override void Init()
        {
            InitList();
            CreateDefaults();
            Behavior.Init();
            Update();
        }

        public override void InitList()
        {
            SettingsList.Clear();
            SettingsList.Add(Assignment);
            SettingsList.Add(StatModifiers);
        }
    }
}