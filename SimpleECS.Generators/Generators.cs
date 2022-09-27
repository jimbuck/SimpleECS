namespace SimpleECS.Generators;

/// <summary>
/// Generates the entity create and query foreach functions
/// Use to increase the default limits
/// </summary>
[Generator]
public sealed class CodeGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context)
    {
        var createEntityFunctions = CreateEntityFunctions(32);
        context.AddSource($"EntityCreateFunctions.g.cs", createEntityFunctions);

        var queryForeachFunctions = CreateQueryFunctions(4, 8);
        context.AddSource($"QueryForeachFunctions.g.cs", queryForeachFunctions);
    }

    string Pattern(string value, int count, bool comma_sep = true)
    {
        string result = "";
        for (int i = 1; i < count; ++i)
        {
            result += value.Replace("#", i.ToString());
            if (comma_sep) result += ", ";
        }
        result += value.Replace("#", (count).ToString());
        return result;
    }

    /// <summary>
    /// Generates all world.CreateEntity functions
    /// </summary>
    /// <param name="count">Maximum number of components that can be used in CreateEntity Functions</param>
    private string CreateEntityFunctions(int count)
    {
        var writer = new StringBuilder();
        writer.AppendLine($"namespace {nameof(SimpleECS)}");
        writer.AppendLine("{");
        writer.AppendLine("  public partial class World");
        writer.AppendLine("  {");

        for (int i = 1; i < count + 1; ++i)
        {
            writer.AppendLine($"      public Entity CreateEntity<{Pattern("C#", i)}>({Pattern("C# c#", i)})");
            writer.AppendLine("      {");
            writer.AppendLine($"          BufferSignature.Clear(){Pattern(".Add<C#>()", i, false)};");
            writer.AppendLine("          var archetype = GetArchetypeData(BufferSignature);");
            writer.AppendLine($"          return StructureEvents.CreateEntity(archetype){Pattern(".Set(c#)", i, false)};");
            writer.AppendLine("      }");
        }
        writer.AppendLine("  }");
        writer.AppendLine("}");

        return writer.ToString();
    }

    /// <summary>
    /// Generates all query.Foreach functions
    /// </summary>
    /// <param name="world_data_count">Maximum number of world data that can be used in queries</param>
    /// <param name="component_count">Maximum number of components that can be used in queries</param>
    private string CreateQueryFunctions(int world_data_count, int component_count)
    {
        var writer = new StringBuilder();
        writer.AppendLine($"namespace {nameof(SimpleECS)}");
        writer.AppendLine("{");
        writer.AppendLine("  public partial class Query");
        writer.AppendLine("  {");

        for (int c = 1; c < component_count + 1; ++c) // components
        {
            string c_val = Pattern("C#", c);
            var arch_get = Pattern("&& archetype.TryGetArray(out C#[] c#)", c, false);

            writer.AppendLine($"       public delegate void c{c}_query<{c_val}>({Pattern("ref C# c#", c)});");
            WriteDocumentation();
            writer.AppendLine($"       public void Foreach<{c_val}>(in c{c}_query<{c_val}> action)");
            writer.AppendLine("       {");
            writer.AppendLine("           CheckQueryChanges();");
            writer.AppendLine("           _world.StructureEvents.EnqueueEvents++;");
            writer.AppendLine("           for (int archetype_index = 0; archetype_index < _archetypeCount; ++archetype_index)");
            writer.AppendLine("           {");
            writer.AppendLine($"               var archetype = _world.Archetypes[_matchingArchetypes[archetype_index]].data;");
            writer.AppendLine("               int count = archetype.EntityCount;");
            writer.AppendLine($"               if (count > 0 {arch_get})");
            writer.AppendLine("               {");
            writer.AppendLine("                   for (int e = 0; e < count; ++e)");
            writer.AppendLine($"                   action({Pattern("ref c#[e]", c)});");
            writer.AppendLine("               }");
            writer.AppendLine("           }");
            writer.AppendLine("           _world.StructureEvents.EnqueueEvents--;");
            writer.AppendLine("       }");
        }

        for (int c = 1; c < component_count + 1; ++c) // entity and components
        {
            string c_val = Pattern("C#", c);
            var arch_get = Pattern("&& archetype.TryGetArray(out C#[] c#)", c, false);

            writer.AppendLine($"       public delegate void ec{c}_query<{c_val}>(Entity entity, {Pattern("ref C# c#", c)});");
            WriteDocumentation();
            writer.AppendLine($"       public void Foreach<{c_val}>(in ec{c}_query<{c_val}> action)");
            writer.AppendLine("       {");
            writer.AppendLine("           CheckQueryChanges();");
            writer.AppendLine("           _world.StructureEvents.EnqueueEvents++;");
            writer.AppendLine("           for (int archetype_index = 0; archetype_index < _archetypeCount; ++archetype_index)");
            writer.AppendLine("           {");
            writer.AppendLine("               var archetype = _world.Archetypes[_matchingArchetypes[archetype_index]].data;");
            writer.AppendLine("               int count = archetype.EntityCount;");
            writer.AppendLine("               var entities = archetype.Entities;");
            writer.AppendLine($"               if (count > 0 {arch_get})");
            writer.AppendLine("               {");
            writer.AppendLine("               for (int e = 0; e < count; ++e)");
            writer.AppendLine($"                   action(entities[e], {Pattern("ref c#[e]", c)});");
            writer.AppendLine("               }");
            writer.AppendLine("           }");
            writer.AppendLine("           _world.StructureEvents.EnqueueEvents--;");
            writer.AppendLine("       }");
        }

        for (int c = 1; c < component_count + 1; ++c) // world data and components
            for (int w = 1; w < world_data_count + 1; ++w)
            {
                string c_val = Pattern("C#", c);
                string w_val = Pattern("W#", w);
                var arch_get = Pattern("&& archetype.TryGetArray(out C#[] c#)", c, false);

                writer.AppendLine($"       public delegate void w{w}c{c}_query<{w_val},{c_val}>({Pattern("in W# w#", w)}, {Pattern("ref C# c#", c)});");
                WriteDocumentation();
                writer.AppendLine($"       public void Foreach<{w_val},{c_val}>(in w{w}c{c}_query<{w_val},{c_val}> action)");
                writer.AppendLine("       {");
                writer.AppendLine("           CheckQueryChanges();");
                writer.AppendLine($"{Pattern("           ref W# w# = ref _world.GetData<W#>();\n", w, false)}");
                writer.AppendLine("           _world.StructureEvents.EnqueueEvents++;");
                writer.AppendLine("           for (int archetype_index = 0; archetype_index < _archetypeCount; ++archetype_index)");
                writer.AppendLine("           {");
                writer.AppendLine("               var archetype = _world.Archetypes[_matchingArchetypes[archetype_index]].data;");
                writer.AppendLine("               int count = archetype.EntityCount;");
                writer.AppendLine($"               if (count > 0 {arch_get})");
                writer.AppendLine("               {");
                writer.AppendLine("                   for (int e = 0; e < count; ++e)");
                writer.AppendLine($"                   action({Pattern("in w#", w)}, {Pattern("ref c#[e]", c)});");
                writer.AppendLine("               }");
                writer.AppendLine("           }");
                writer.AppendLine("           _world.StructureEvents.EnqueueEvents--;");
                writer.AppendLine("       }");
            }

        for (int c = 1; c < component_count + 1; ++c) // world data, entity and components
            for (int w = 1; w < world_data_count + 1; ++w)
            {
                string c_val = Pattern("C#", c);
                string w_val = Pattern("W#", w);
                var arch_get = Pattern("&& archetype.TryGetArray(out C#[] c#)", c, false);

                writer.AppendLine($"       public delegate void w{w}ec{c}_query<{w_val},{c_val}>({Pattern("in W# w#", w)}, Entity entity, {Pattern("ref C# c#", c)});");
                WriteDocumentation();
                writer.AppendLine($"       public void Foreach<{w_val},{c_val}>(in w{w}ec{c}_query<{w_val},{c_val}> action)");
                writer.AppendLine("       {");
                writer.AppendLine("           CheckQueryChanges();");
                writer.AppendLine($"{Pattern("           ref W# w# = ref _world.GetData<W#>();\n", w, false)}");
                writer.AppendLine("           _world.StructureEvents.EnqueueEvents++;");
                writer.AppendLine("           for (int archetype_index = 0; archetype_index < _archetypeCount; ++archetype_index)");
                writer.AppendLine("           {");
                writer.AppendLine("               var archetype = _world.Archetypes[_matchingArchetypes[archetype_index]].data;");
                writer.AppendLine("               int count = archetype.EntityCount;");
                writer.AppendLine("               var entities = archetype.Entities;");
                writer.AppendLine($"               if (count > 0 {arch_get})");
                writer.AppendLine("               {");
                writer.AppendLine("                   for (int e = 0; e < count; ++e)");
                writer.AppendLine($"                   action({Pattern("in w#", w)}, entities[e], {Pattern("ref c#[e]", c)});");
                writer.AppendLine("               }");
                writer.AppendLine("           }");
                writer.AppendLine("           _world.StructureEvents.EnqueueEvents--;");
                writer.AppendLine("       }");
            }

        writer.AppendLine("  }");
        writer.AppendLine("}");

        void WriteDocumentation()
        {
            writer.AppendLine("        /// <summary>");
            writer.AppendLine("        /// performs the action on all entities that match the query.");
            writer.AppendLine("        /// query must be in the form of '(in world_data', entity, ref components)'.");
            writer.AppendLine($"        /// query can add up to {world_data_count} world data and {component_count} components");
            writer.AppendLine("        /// </summary>");
        }

        return writer.ToString();
    }
}