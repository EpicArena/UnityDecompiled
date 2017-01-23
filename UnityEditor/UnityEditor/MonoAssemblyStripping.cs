using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.Utils;

namespace UnityEditor
{
	internal class MonoAssemblyStripping
	{
		private class AssemblyDefinitionComparer : IEqualityComparer<AssemblyDefinition>
		{
			public bool Equals(AssemblyDefinition x, AssemblyDefinition y)
			{
				return x.get_FullName() == y.get_FullName();
			}

			public int GetHashCode(AssemblyDefinition obj)
			{
				return obj.get_FullName().GetHashCode();
			}
		}

		private static void ReplaceFile(string src, string dst)
		{
			if (File.Exists(dst))
			{
				FileUtil.DeleteFileOrDirectory(dst);
			}
			FileUtil.CopyFileOrDirectory(src, dst);
		}

		public static void MonoCilStrip(BuildTarget buildTarget, string managedLibrariesDirectory, string[] fileNames)
		{
			string buildToolsDirectory = BuildPipeline.GetBuildToolsDirectory(buildTarget);
			string str = Path.Combine(buildToolsDirectory, "mono-cil-strip.exe");
			for (int i = 0; i < fileNames.Length; i++)
			{
				string text = fileNames[i];
				Process process = MonoProcessUtility.PrepareMonoProcess(buildTarget, managedLibrariesDirectory);
				string text2 = text + ".out";
				process.StartInfo.Arguments = "\"" + str + "\"";
				ProcessStartInfo expr_5E = process.StartInfo;
				string arguments = expr_5E.Arguments;
				expr_5E.Arguments = string.Concat(new string[]
				{
					arguments,
					" \"",
					text,
					"\" \"",
					text,
					".out\""
				});
				MonoProcessUtility.RunMonoProcess(process, "byte code stripper", Path.Combine(managedLibrariesDirectory, text2));
				MonoAssemblyStripping.ReplaceFile(managedLibrariesDirectory + "/" + text2, managedLibrariesDirectory + "/" + text);
				File.Delete(managedLibrariesDirectory + "/" + text2);
			}
		}

		public static string GenerateBlackList(string librariesFolder, RuntimeClassRegistry usedClasses, string[] allAssemblies)
		{
			string text = "tmplink.xml";
			usedClasses.SynchronizeClasses();
			using (TextWriter textWriter = new StreamWriter(Path.Combine(librariesFolder, text)))
			{
				textWriter.WriteLine("<linker>");
				textWriter.WriteLine("<assembly fullname=\"UnityEngine\">");
				foreach (string current in usedClasses.GetAllManagedClassesAsString())
				{
					textWriter.WriteLine(string.Format("<type fullname=\"UnityEngine.{0}\" preserve=\"{1}\"/>", current, usedClasses.GetRetentionLevel(current)));
				}
				textWriter.WriteLine("</assembly>");
				DefaultAssemblyResolver defaultAssemblyResolver = new DefaultAssemblyResolver();
				defaultAssemblyResolver.AddSearchDirectory(librariesFolder);
				for (int i = 0; i < allAssemblies.Length; i++)
				{
					string path = allAssemblies[i];
					BaseAssemblyResolver arg_CB_0 = defaultAssemblyResolver;
					string arg_CB_1 = Path.GetFileNameWithoutExtension(path);
					ReaderParameters readerParameters = new ReaderParameters();
					readerParameters.set_AssemblyResolver(defaultAssemblyResolver);
					AssemblyDefinition assemblyDefinition = arg_CB_0.Resolve(arg_CB_1, readerParameters);
					textWriter.WriteLine("<assembly fullname=\"{0}\">", assemblyDefinition.get_Name().get_Name());
					if (assemblyDefinition.get_Name().get_Name().StartsWith("UnityEngine."))
					{
						foreach (string current2 in usedClasses.GetAllManagedClassesAsString())
						{
							textWriter.WriteLine(string.Format("<type fullname=\"UnityEngine.{0}\" preserve=\"{1}\"/>", current2, usedClasses.GetRetentionLevel(current2)));
						}
					}
					MonoAssemblyStripping.GenerateBlackListTypeXML(textWriter, assemblyDefinition.get_MainModule().get_Types(), usedClasses.GetAllManagedBaseClassesAsString());
					textWriter.WriteLine("</assembly>");
				}
				textWriter.WriteLine("</linker>");
			}
			return text;
		}

		public static string GenerateLinkXmlToPreserveDerivedTypes(string stagingArea, string librariesFolder, RuntimeClassRegistry usedClasses)
		{
			string fullPath = Path.GetFullPath(Path.Combine(stagingArea, "preserved_derived_types.xml"));
			DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
			resolver.AddSearchDirectory(librariesFolder);
			using (TextWriter textWriter = new StreamWriter(fullPath))
			{
				textWriter.WriteLine("<linker>");
				foreach (AssemblyDefinition current in MonoAssemblyStripping.CollectAssembliesRecursive((from s in usedClasses.GetUserAssemblies()
				where usedClasses.IsDLLUsed(s)
				select s).Select(delegate(string file)
				{
					BaseAssemblyResolver arg_1F_0 = resolver;
					string arg_1F_1 = Path.GetFileNameWithoutExtension(file);
					ReaderParameters readerParameters = new ReaderParameters();
					readerParameters.set_AssemblyResolver(resolver);
					return arg_1F_0.Resolve(arg_1F_1, readerParameters);
				})))
				{
					if (!(current.get_Name().get_Name() == "UnityEngine"))
					{
						HashSet<TypeDefinition> hashSet = new HashSet<TypeDefinition>();
						MonoAssemblyStripping.CollectBlackListTypes(hashSet, current.get_MainModule().get_Types(), usedClasses.GetAllManagedBaseClassesAsString());
						if (hashSet.Count != 0)
						{
							textWriter.WriteLine("<assembly fullname=\"{0}\">", current.get_Name().get_Name());
							foreach (TypeDefinition current2 in hashSet)
							{
								textWriter.WriteLine("<type fullname=\"{0}\" preserve=\"all\"/>", current2.get_FullName());
							}
							textWriter.WriteLine("</assembly>");
						}
					}
				}
				textWriter.WriteLine("</linker>");
			}
			return fullPath;
		}

		private static HashSet<AssemblyDefinition> CollectAssembliesRecursive(IEnumerable<AssemblyDefinition> assemblies)
		{
			HashSet<AssemblyDefinition> hashSet = new HashSet<AssemblyDefinition>(assemblies, new MonoAssemblyStripping.AssemblyDefinitionComparer());
			int num = 0;
			while (hashSet.Count > num)
			{
				num = hashSet.Count;
				hashSet.UnionWith(hashSet.ToArray<AssemblyDefinition>().SelectMany((AssemblyDefinition assembly) => from a in assembly.get_MainModule().get_AssemblyReferences()
				select assembly.get_MainModule().get_AssemblyResolver().Resolve(a)));
			}
			return hashSet;
		}

		private static void CollectBlackListTypes(HashSet<TypeDefinition> typesToPreserve, IList<TypeDefinition> types, List<string> baseTypes)
		{
			if (types != null)
			{
				foreach (TypeDefinition current in types)
				{
					if (current != null)
					{
						foreach (string current2 in baseTypes)
						{
							if (MonoAssemblyStripping.DoesTypeEnheritFrom(current, current2))
							{
								typesToPreserve.Add(current);
								break;
							}
						}
						MonoAssemblyStripping.CollectBlackListTypes(typesToPreserve, current.get_NestedTypes(), baseTypes);
					}
				}
			}
		}

		private static void GenerateBlackListTypeXML(TextWriter w, IList<TypeDefinition> types, List<string> baseTypes)
		{
			HashSet<TypeDefinition> hashSet = new HashSet<TypeDefinition>();
			MonoAssemblyStripping.CollectBlackListTypes(hashSet, types, baseTypes);
			foreach (TypeDefinition current in hashSet)
			{
				w.WriteLine("<type fullname=\"{0}\" preserve=\"all\"/>", current.get_FullName());
			}
		}

		private static bool DoesTypeEnheritFrom(TypeReference type, string typeName)
		{
			bool result;
			while (type != null)
			{
				if (type.get_FullName() == typeName)
				{
					result = true;
				}
				else
				{
					TypeDefinition typeDefinition = type.Resolve();
					if (typeDefinition != null)
					{
						type = typeDefinition.get_BaseType();
						continue;
					}
					result = false;
				}
				return result;
			}
			result = false;
			return result;
		}

		private static string StripperExe()
		{
			return "Tools/UnusedBytecodeStripper.exe";
		}

		public static void MonoLink(BuildTarget buildTarget, string managedLibrariesDirectory, string[] input, string[] allAssemblies, RuntimeClassRegistry usedClasses)
		{
			Process process = MonoProcessUtility.PrepareMonoProcess(buildTarget, managedLibrariesDirectory);
			string buildToolsDirectory = BuildPipeline.GetBuildToolsDirectory(buildTarget);
			string text = null;
			string frameWorksFolder = MonoInstallationFinder.GetFrameWorksFolder();
			string text2 = Path.Combine(frameWorksFolder, MonoAssemblyStripping.StripperExe());
			string text3 = Path.Combine(Path.GetDirectoryName(text2), "link.xml");
			string text4 = Path.Combine(managedLibrariesDirectory, "output");
			Directory.CreateDirectory(text4);
			process.StartInfo.Arguments = "\"" + text2 + "\" -l none -c link";
			for (int i = 0; i < input.Length; i++)
			{
				string str = input[i];
				ProcessStartInfo expr_82 = process.StartInfo;
				expr_82.Arguments = expr_82.Arguments + " -a \"" + str + "\"";
			}
			ProcessStartInfo expr_B5 = process.StartInfo;
			string arguments = expr_B5.Arguments;
			expr_B5.Arguments = string.Concat(new string[]
			{
				arguments,
				" -out output -x \"",
				text3,
				"\" -d \"",
				managedLibrariesDirectory,
				"\""
			});
			string text5 = Path.Combine(buildToolsDirectory, "link.xml");
			if (File.Exists(text5))
			{
				ProcessStartInfo expr_112 = process.StartInfo;
				expr_112.Arguments = expr_112.Arguments + " -x \"" + text5 + "\"";
			}
			string text6 = Path.Combine(Path.GetDirectoryName(text2), "Core.xml");
			if (File.Exists(text6))
			{
				ProcessStartInfo expr_153 = process.StartInfo;
				expr_153.Arguments = expr_153.Arguments + " -x \"" + text6 + "\"";
			}
			string[] files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "Assets"), "link.xml", SearchOption.AllDirectories);
			string[] array = files;
			for (int j = 0; j < array.Length; j++)
			{
				string str2 = array[j];
				ProcessStartInfo expr_1A5 = process.StartInfo;
				expr_1A5.Arguments = expr_1A5.Arguments + " -x \"" + str2 + "\"";
			}
			if (usedClasses != null)
			{
				text = MonoAssemblyStripping.GenerateBlackList(managedLibrariesDirectory, usedClasses, allAssemblies);
				ProcessStartInfo expr_1EA = process.StartInfo;
				expr_1EA.Arguments = expr_1EA.Arguments + " -x \"" + text + "\"";
			}
			string path = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(EditorUserBuildSettings.activeBuildTarget, BuildOptions.None), "Whitelists");
			string[] files2 = Directory.GetFiles(path, "*.xml");
			for (int k = 0; k < files2.Length; k++)
			{
				string str3 = files2[k];
				ProcessStartInfo expr_241 = process.StartInfo;
				expr_241.Arguments = expr_241.Arguments + " -x \"" + str3 + "\"";
			}
			MonoProcessUtility.RunMonoProcess(process, "assemblies stripper", Path.Combine(text4, "mscorlib.dll"));
			MonoAssemblyStripping.DeleteAllDllsFrom(managedLibrariesDirectory);
			MonoAssemblyStripping.CopyAllDlls(managedLibrariesDirectory, text4);
			string[] files3 = Directory.GetFiles(managedLibrariesDirectory);
			for (int l = 0; l < files3.Length; l++)
			{
				string text7 = files3[l];
				if (text7.Contains(".mdb"))
				{
					string path2 = text7.Replace(".mdb", "");
					if (!File.Exists(path2))
					{
						FileUtil.DeleteFileOrDirectory(text7);
					}
				}
			}
			if (text != null)
			{
				FileUtil.DeleteFileOrDirectory(Path.Combine(managedLibrariesDirectory, text));
			}
			FileUtil.DeleteFileOrDirectory(text4);
		}

		private static void CopyFiles(IEnumerable<string> files, string fromDir, string toDir)
		{
			foreach (string current in files)
			{
				FileUtil.ReplaceFile(Path.Combine(fromDir, current), Path.Combine(toDir, current));
			}
		}

		private static void CopyAllDlls(string fromDir, string toDir)
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(toDir);
			FileInfo[] files = directoryInfo.GetFiles("*.dll");
			FileInfo[] array = files;
			for (int i = 0; i < array.Length; i++)
			{
				FileInfo fileInfo = array[i];
				FileUtil.ReplaceFile(Path.Combine(toDir, fileInfo.Name), Path.Combine(fromDir, fileInfo.Name));
			}
		}

		private static void DeleteAllDllsFrom(string managedLibrariesDirectory)
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(managedLibrariesDirectory);
			FileInfo[] files = directoryInfo.GetFiles("*.dll");
			FileInfo[] array = files;
			for (int i = 0; i < array.Length; i++)
			{
				FileInfo fileInfo = array[i];
				FileUtil.DeleteFileOrDirectory(fileInfo.FullName);
			}
		}
	}
}
