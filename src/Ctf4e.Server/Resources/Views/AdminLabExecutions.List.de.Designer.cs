﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
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
    internal class AdminLabExecutions_List_de {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AdminLabExecutions_List_de() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Views.AdminLabExecutions.List.de", typeof(AdminLabExecutions_List_de).Assembly);
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
        ///   Looks up a localized string similar to Aktionen.
        /// </summary>
        internal static string Actions {
            get {
                return ResourceManager.GetString("Actions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Abbrechen.
        /// </summary>
        internal static string Cancel {
            get {
                return ResourceManager.GetString("Cancel", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Löschen.
        /// </summary>
        internal static string Delete {
            get {
                return ResourceManager.GetString("Delete", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Bearbeiten.
        /// </summary>
        internal static string Edit {
            get {
                return ResourceManager.GetString("Edit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;p&gt;
        ///    Praktikumsausführungen steuern, wann ein Praktikumstermin startet und endet. Hierfür gibt es folgende Regeln:
        ///&lt;/p&gt;
        ///&lt;ul&gt;
        ///    &lt;li&gt;&lt;strong&gt;Beginn:&lt;/strong&gt; Ab diesem Zeitpunkt sind sämtliche Aufgaben freigeschaltet. Die Gruppen können in diesem Zeitraum das Praktikum bestehen.&lt;/li&gt;
        ///    &lt;li&gt;&lt;strong&gt;Ende:&lt;/strong&gt; Nach diesem Zeitpunkt können zwar noch Aufgaben bearbeitet werden, diese werden jedoch nicht mehr im Scoreboard gewertet. Flags können nicht mehr eingelöst werden.&lt;/li&gt;
        ///&lt;/ul&gt;
        ///&lt;p&gt;
        ///    Di [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string Explanation {
            get {
                return ResourceManager.GetString("Explanation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Beginn.
        /// </summary>
        internal static string LabExecutions_Begin {
            get {
                return ResourceManager.GetString("LabExecutions:Begin", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ende.
        /// </summary>
        internal static string LabExecutions_End {
            get {
                return ResourceManager.GetString("LabExecutions:End", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Gruppe.
        /// </summary>
        internal static string LabExecutions_Group {
            get {
                return ResourceManager.GetString("LabExecutions:Group", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Filtern (regex).
        /// </summary>
        internal static string LabExecutions_Group_Filter {
            get {
                return ResourceManager.GetString("LabExecutions:Group:Filter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Praktikum.
        /// </summary>
        internal static string LabExecutions_Lab {
            get {
                return ResourceManager.GetString("LabExecutions:Lab", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Filtern (regex).
        /// </summary>
        internal static string LabExecutions_Lab_Filter {
            get {
                return ResourceManager.GetString("LabExecutions:Lab:Filter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;p&gt;Wollen Sie die Ausführung des Praktikums &amp;quot;&lt;span data-replace=&quot;lab-name&quot;&gt;&lt;/span&gt;&amp;quot; für Gruppe &amp;quot;&lt;span data-replace=&quot;group-name&quot;&gt;&lt;/span&gt;&amp;quot; wirklich löschen?&lt;/p&gt;&lt;p&gt;Die Gruppe kann dann keine Aufgaben mehr bearbeiten, und sämtliche existierenden Einreichungen werden nicht mehr gewertet.&lt;/p&gt;.
        /// </summary>
        internal static string Modal_DeleteLabExecutionGroup_Body {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionGroup:Body", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ausführung löschen.
        /// </summary>
        internal static string Modal_DeleteLabExecutionGroup_Submit {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionGroup:Submit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ausführung für Gruppe löschen.
        /// </summary>
        internal static string Modal_DeleteLabExecutionGroup_Title {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionGroup:Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;p&gt;Hinweis: Die betroffenen Gruppen können dann keine Aufgaben mehr bearbeiten, und der &amp;quot;Bestanden&amp;quot;-Status wird zurückgesetzt.&lt;/p&gt;.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Body {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Body", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Praktikum.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Lab {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Lab", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Praktikum auswählen.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Lab_Placeholder {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Lab:Placeholder", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Terminslot.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Slot {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Slot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Slot auswählen.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Form_Slot_Placeholder {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Form:Slot:Placeholder", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ausführungen löschen.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Submit {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Submit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Alle Ausführungen für Terminslot löschen.
        /// </summary>
        internal static string Modal_DeleteLabExecutionsSlot_Title {
            get {
                return ResourceManager.GetString("Modal:DeleteLabExecutionsSlot:Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Praktikum für eine Gruppe starten.
        /// </summary>
        internal static string StartForOneGroup {
            get {
                return ResourceManager.GetString("StartForOneGroup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Praktikum für alle Gruppen eines Termins starten / anpassen.
        /// </summary>
        internal static string StartForSlot {
            get {
                return ResourceManager.GetString("StartForSlot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Praktikum für alle Gruppen eines Termins abbrechen.
        /// </summary>
        internal static string StopForSlot {
            get {
                return ResourceManager.GetString("StopForSlot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Aktive Praktika verwalten.
        /// </summary>
        internal static string Title {
            get {
                return ResourceManager.GetString("Title", resourceCulture);
            }
        }
    }
}
