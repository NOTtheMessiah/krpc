using System;
using System.Collections.Generic;
using System.Reflection;

namespace KRPC.Service.Scanner
{
    class ServiceSignature
    {
        /// <summary>
        /// The name of the service
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// A mapping from procedure names to signatures for all RPCs in this service
        /// </summary>
        public Dictionary<string,ProcedureSignature> Procedures { get; private set; }

        /// <summary>
        /// The names of all classes defined in this service
        /// </summary>
        public HashSet<string> Classes { get; private set; }

        /// <summary>
        /// The names of all C# defined enums defined in this service, and their allowed values
        /// </summary>
        public Dictionary<string,Dictionary<string,int>> Enums { get; private set; }

        /// <summary>
        /// Which game scene(s) the service should be active during
        /// </summary>
        public GameScene GameScene { get; private set; }

        /// <summary>
        /// Create a service signature from a C# type annotated with the KRPCService attribute
        /// </summary>
        /// <param name="type">Type.</param>
        public ServiceSignature (Type type)
        {
            TypeUtils.ValidateKRPCService (type);
            Name = TypeUtils.GetServiceName (type);
            Classes = new HashSet<string> ();
            Enums = new Dictionary<string, Dictionary<string, int>> ();
            Procedures = new Dictionary<string, ProcedureSignature> ();
            GameScene = TypeUtils.GetServiceGameScene (type);
        }

        /// <summary>
        /// Create a service with the given name.
        /// </summary>
        public ServiceSignature (string name)
        {
            Name = name;
            Classes = new HashSet<string> ();
            Procedures = new Dictionary<string, ProcedureSignature> ();
        }

        /// <summary>
        /// Add a procedure to the service
        /// </summary>
        void AddProcedure (ProcedureSignature signature)
        {
            if (Procedures.ContainsKey (signature.Name))
                throw new ServiceException ("Service " + Name + " contains duplicate procedures " + signature.Name);
            Procedures [signature.Name] = signature;
        }

        /// <summary>
        /// Add a procedure to the service for the given method annotated with the KRPCProcedure attribute.
        /// </summary>
        public void AddProcedure (MethodInfo method)
        {
            TypeUtils.ValidateKRPCProcedure (method);
            AddProcedure (new ProcedureSignature (Name, method.Name, new ProcedureHandler (method), GameScene));
        }

        /// <summary>
        /// Add a property to the service for the given property annotated with the KRPCProperty attribute.
        /// </summary>
        public void AddProperty (PropertyInfo property)
        {
            TypeUtils.ValidateKRPCProperty (property);
            if (property.GetGetMethod () != null) {
                var method = property.GetGetMethod ();
                var handler = new ProcedureHandler (method);
                var attribute = "Property.Get(" + property.Name + ")";
                AddProcedure (new ProcedureSignature (Name, method.Name, handler, GameScene, attribute));
            }
            if (property.GetSetMethod () != null) {
                var method = property.GetSetMethod ();
                var handler = new ProcedureHandler (method);
                var attribute = "Property.Set(" + property.Name + ")";
                AddProcedure (new ProcedureSignature (Name, method.Name, handler, GameScene, attribute));
            }
        }

        /// <summary>
        /// Add a class to the service for the given class type annotated with the KRPCClass attribute.
        /// </summary>
        public string AddClass (Type classType)
        {
            TypeUtils.ValidateKRPCClass (classType);
            var name = classType.Name;
            if (Classes.Contains (name))
                throw new ServiceException ("Service " + Name + " contains duplicate classes " + name);
            Classes.Add (name);
            return name;
        }

        /// <summary>
        /// Add an enum to the service for the given enum type annotated with the KRPCEnum attribute.
        /// </summary>
        public IDictionary<string,int> AddEnum (Type enumType)
        {
            TypeUtils.ValidateKRPCEnum (enumType);
            var name = enumType.Name;
            if (Enums.ContainsKey (name))
                throw new ServiceException ("Service " + Name + " contains duplicate enumerations " + name);
            Enums [enumType.Name] = new Dictionary<string, int> ();
            foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                Enums [enumType.Name] [field.Name] = (int)field.GetRawConstantValue ();
            }
            return Enums [enumType.Name];
        }

        /// <summary>
        /// Add a class method to the given class in the given service for the given class type annotated with the KRPCClass attribute.
        /// </summary>
        public void AddClassMethod (string cls, MethodInfo method)
        {
            if (!Classes.Contains (cls))
                throw new ArgumentException ("Class " + cls + " does not exist");
            if (!method.IsStatic) {
                var handler = new ClassMethodHandler (method);
                AddProcedure (new ProcedureSignature (Name, cls + '_' + method.Name, handler, GameScene,
                    "Class.Method(" + Name + "." + cls + "," + method.Name + ")", "ParameterType(0).Class(" + Name + "." + cls + ")"));
            } else {
                var handler = new ClassStaticMethodHandler (method);
                AddProcedure (new ProcedureSignature (Name, cls + '_' + method.Name, handler, GameScene,
                    "Class.StaticMethod(" + Name + "." + cls + "," + method.Name + ")"));
            }
        }

        /// <summary>
        /// Add a class property to the given class in the given service for the given property annotated with the KRPCProperty attribute.
        /// </summary>
        public void AddClassProperty (string cls, PropertyInfo property)
        {
            if (!Classes.Contains (cls))
                throw new ArgumentException ("Class " + cls + " does not exist");
            if (property.GetGetMethod () != null) {
                var method = property.GetGetMethod ();
                var handler = new ClassMethodHandler (method);
                var attribute = "Class.Property.Get(" + Name + "." + cls + "," + property.Name + ")";
                var parameter_attribute = "ParameterType(0).Class(" + Name + "." + cls + ")";
                AddProcedure (new ProcedureSignature (Name, cls + '_' + method.Name, handler, GameScene, attribute, parameter_attribute));
            }
            if (property.GetSetMethod () != null) {
                var method = property.GetSetMethod ();
                var handler = new ClassMethodHandler (method);
                var attribute = "Class.Property.Set(" + Name + "." + cls + "," + property.Name + ")";
                var parameter_attribute = "ParameterType(0).Class(" + Name + "." + cls + ")";
                AddProcedure (new ProcedureSignature (Name, cls + '_' + method.Name, handler, GameScene, attribute, parameter_attribute));
            }
        }
    }
}
