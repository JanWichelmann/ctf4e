﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Ctf4e.Server.Resources.Controllers {
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
    internal class AdminExercisesController_de {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AdminExercisesController_de() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Ctf4e.Server.Resources.Controllers.AdminExercisesController.de", typeof(AdminExercisesController_de).Assembly);
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
        ///   Looks up a localized string similar to Ungültige Eingabe..
        /// </summary>
        internal static string CreateExerciseAsync_InvalidInput {
            get {
                return ResourceManager.GetString("CreateExerciseAsync:InvalidInput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Die Aufgabe wurde erfolgreich erstellt..
        /// </summary>
        internal static string CreateExerciseAsync_Success {
            get {
                return ResourceManager.GetString("CreateExerciseAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Beim Erstellen der Aufgabe ist ein Fehler aufgetreten. Weitere Details finden sich im Log..
        /// </summary>
        internal static string CreateExerciseAsync_UnknownError {
            get {
                return ResourceManager.GetString("CreateExerciseAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Die Aufgabe wurde erfolgreich gelöscht..
        /// </summary>
        internal static string DeleteExerciseAsync_Success {
            get {
                return ResourceManager.GetString("DeleteExerciseAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Beim Löschen der Aufgabe ist ein Fehler aufgetreten. Weitere Details finden sich im Log..
        /// </summary>
        internal static string DeleteExerciseAsync_UnknownError {
            get {
                return ResourceManager.GetString("DeleteExerciseAsync:UnknownError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ungültige Eingabe..
        /// </summary>
        internal static string EditExerciseAsync_InvalidInput {
            get {
                return ResourceManager.GetString("EditExerciseAsync:InvalidInput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Die Aufgabe wurde erfolgreich aktualisiert..
        /// </summary>
        internal static string EditExerciseAsync_Success {
            get {
                return ResourceManager.GetString("EditExerciseAsync:Success", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Beim Aktualisieren der Aufgabe ist ein Fehler aufgetreten. Weitere Details finden sich im Log..
        /// </summary>
        internal static string EditExerciseAsync_UnknownError {
            get {
                return ResourceManager.GetString("EditExerciseAsync:UnknownError", resourceCulture);
            }
        }
    }
}