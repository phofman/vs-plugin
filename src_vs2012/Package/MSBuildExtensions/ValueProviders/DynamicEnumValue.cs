﻿using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using System.Collections.Generic;

namespace BlackBerry.Package.MSBuildExtensions.ValueProviders
{
    sealed class DynamicEnumValue : IEnumValue
    {
        public DynamicEnumValue()
        {
        }

        public DynamicEnumValue(string name, string displayName, string description, bool isDefault)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
            IsDefault = isDefault;
        }

        public string Name
        {
            get;
            set;
        }

        public string DisplayName
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string HelpString
        {
            get;
            set;
        }

        public string Switch
        {
            get;
            set;
        }

        public string SwitchPrefix
        {
            get;
            set;
        }

        public bool IsDefault
        {
            get;
            set;
        }

        public IList<NameValuePair> Metadata
        {
            get;
            set;
        }

        public IList<IArgument> Arguments
        {
            get;
            set;
        }
    }
}
