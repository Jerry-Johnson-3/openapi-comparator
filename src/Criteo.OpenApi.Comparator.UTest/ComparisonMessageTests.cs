// Copyright (c) Criteo Technology. All rights reserved.
// Licensed under the Apache 2.0 License. See LICENSE in the project root for license information.

using System.Linq;
using Criteo.OpenApi.Comparator.Logging;
using Criteo.OpenApi.Comparator.Parser;
using NUnit.Framework;

namespace Criteo.OpenApi.Comparator.UTest
{
    [TestFixture]
    public class ComparisonMessageTests
    {
        private const string PetPointer =
            "#/paths/~1pets/get/responses/200/content/application~1json/schema/properties/pet";

        [TestCase(true)]
        [TestCase(false)]
        public void JsonRefs_FollowPath_WhenReferenceDoesNotResolveInThatDocument(bool missingInOld)
        {
            var resolvable = Spec("                    $ref: '#/components/schemas/Pet'\n");
            var dangling = Spec("                    $ref: '#/components/schemas/Missing'\n");
            var message = Message(missingInOld ? dangling : resolvable, missingInOld ? resolvable : dangling,
                "properties", "id");

            var (missingRef, missingPath, missingJson) = missingInOld
                ? (message.OldJsonRef, message.OldJsonPath, message.OldJson())
                : (message.NewJsonRef, message.NewJsonPath, message.NewJson());
            var presentPath = missingInOld ? message.NewJsonPath : message.OldJsonPath;

            Assert.That(missingRef, Is.EqualTo($"{PetPointer}/properties/id"));
            Assert.That(missingPath, Is.EqualTo("#/components/schemas/Missing/properties/id"));
            Assert.That(missingJson.Value<string>("$ref"), Is.EqualTo("#/components/schemas/Missing"));
            Assert.That(presentPath, Is.EqualTo("#/components/schemas/Pet/properties/id"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void JsonRefs_FollowPath_WhenArrayIndexDoesNotExistInThatDocument(bool missingInOld)
        {
            var twoItems = Spec(
@"                    oneOf:
                      - $ref: '#/components/schemas/Pet'
                      - $ref: '#/components/schemas/Pet'
");
            var oneItem = Spec(
@"                    oneOf:
                      - $ref: '#/components/schemas/Pet'
");
            var message = Message(missingInOld ? oneItem : twoItems, missingInOld ? twoItems : oneItem,
                "oneOf", "1", "properties", "id");

            var (missingRef, missingPath, missingJson) = missingInOld
                ? (message.OldJsonRef, message.OldJsonPath, message.OldJson())
                : (message.NewJsonRef, message.NewJsonPath, message.NewJson());
            var presentPath = missingInOld ? message.NewJsonPath : message.OldJsonPath;

            Assert.That(missingRef, Is.EqualTo($"{PetPointer}/oneOf/1/properties/id"));
            Assert.That(missingPath, Is.EqualTo($"{PetPointer}/oneOf/1/properties/id"));
            Assert.That(missingJson.Count(), Is.EqualTo(1));
            Assert.That(presentPath, Is.EqualTo("#/components/schemas/Pet/properties/id"));
        }

        [Test]
        public void Compare_ReturnsReadableJsonRefs_WhenOneOfReferenceMovesToLowerIndex()
        {
            var oldSpec = Spec(
@"                    oneOf:
                      - $ref: '#/components/schemas/Cat'
                      - $ref: '#/components/schemas/Pet'
",
@"    Cat:
      type: object
");
            var newSpec = Spec(
@"                    oneOf:
                      - $ref: '#/components/schemas/Pet'
",
@"        name:
          type: string
");

            var messages = OpenApiComparator.Compare(oldSpec, newSpec, out _).ToList();

            Assert.That(messages, Has.Some.Matches<ComparisonMessage>(m =>
                m.Code == nameof(ComparisonRules.AddedPropertyInResponse)));
            Assert.That(() => messages.Select(m => (m.OldJsonRef, m.NewJsonRef, m.OldJsonPath, m.NewJsonPath)).ToList(),
                Throws.Nothing);
        }

        private static ComparisonMessage Message(string oldSpec, string newSpec, params string[] pathBelowPet)
        {
            var path = new[] { "paths", "/pets", "get", "responses", "200", "content", "application/json", "schema", "properties", "pet" }
                .Concat(pathBelowPet)
                .Aggregate(ObjectPath.Empty, (objectPath, property) => objectPath.AppendProperty(property));
            return new ComparisonMessage(ComparisonRules.TypeChanged, path,
                OpenApiParser.Parse(oldSpec, out _), OpenApiParser.Parse(newSpec, out _), Severity.Warning, "object", "object");
        }

        private static string Spec(string petSchema, string extraComponentLines = "") =>
@"openapi: 3.0.0
info:
  title: t
  version: ""1.0""
paths:
  /pets:
    get:
      responses:
        ""200"":
          description: ok
          content:
            application/json:
              schema:
                type: object
                properties:
                  pet:
" + petSchema +
@"components:
  schemas:
    Pet:
      type: object
      properties:
        id:
          type: integer
" + extraComponentLines;
    }
}
