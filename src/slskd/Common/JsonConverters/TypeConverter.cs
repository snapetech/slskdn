// <copyright file="TypeConverter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class TypeConverter : JsonConverter<Type?>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Type);

    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var typeName = reader.GetString();
        return typeName is null ? null : Type.GetType(typeName);
    }

    public override void Write(Utf8JsonWriter writer, Type? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.AssemblyQualifiedName);
    }
}
