﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Ctf4e.Server.Resources.Views {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class AdminScoreboard_Sync_de {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AdminScoreboard_Sync_de() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Views.AdminScoreboard.Sync.de", typeof(AdminScoreboard_Sync_de).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cancel.
        /// </summary>
        internal static string Cancel {
            get {
                return ResourceManager.GetString("Cancel", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ergebnisse als CSV herunterladen.
        /// </summary>
        internal static string DownloadCsv {
            get {
                return ResourceManager.GetString("DownloadCsv", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;p&gt;Dieses Kommando lädt die kompletten Kursergebnisse über die API in den angebundenen Moodlekurs hoch.&lt;/p&gt;&lt;p&gt;&lt;strong&gt;Achtung:&lt;/strong&gt; Dieser Vorgang dauert einige Zeit, und sollte nicht unterbrochen werden.&lt;/p&gt;.
        /// </summary>
        internal static string Modal_SyncMoodle_Body {
            get {
                return ResourceManager.GetString("Modal:SyncMoodle:Body", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ergebnisse hochladen.
        /// </summary>
        internal static string Modal_SyncMoodle_Submit {
            get {
                return ResourceManager.GetString("Modal:SyncMoodle:Submit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ergebnisse in Moodlekurs hochladen.
        /// </summary>
        internal static string Modal_SyncMoodle_Title {
            get {
                return ResourceManager.GetString("Modal:SyncMoodle:Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Export / Synchronisation.
        /// </summary>
        internal static string Title {
            get {
                return ResourceManager.GetString("Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ergebnisse in Moodlekurs hochladen.
        /// </summary>
        internal static string UploadMoodle {
            get {
                return ResourceManager.GetString("UploadMoodle", resourceCulture);
            }
        }
    }
}