using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DocParser.UnitTests
{
    [TestClass]
    public class TestParser
    {
        [TestMethod]
        public void TestFileDescription()
        {
            JSDocParser parser = new JSDocParser();

            var parse = parser.Parse("TestFiles\\fileSummaryOnly.js", true);

            Assert.IsNotNull(parse);

            Assert.IsNotNull(parse.File);

            Assert.IsNotNull(parse.File.Description, "Missing file description.");

            Assert.AreEqual("This is a file description.", parse.File.Description, "Expected File Description text to match.");
        }

        [TestMethod]
        public void TestFunctionParsing()
        {
            JSDocParser parser = new JSDocParser();

            var parse = parser.Parse("TestFiles\\compactOverlay.js", true);

            Assert.IsNotNull(parse);

            Assert.IsNull(parse.File);
            Assert.IsNotNull(parse.Functions);

            Assert.AreEqual(1, parse.Functions.Count, "Expected Function Overview");

            var function = parse.Functions.First();

            Assert.AreEqual("Toggle Compact Overlay mode", function.Name, "Alias didn't parse.");

            Assert.AreEqual("toggleCompactOverlayMode", function.Method, "Method didn't parse.");

            Assert.AreEqual("When an app window enters compact overlay mode it’ll be shown above other windows so it won’t get blocked. This allows users to continue to keep an eye on your app's content even when they are working with something else. The canonical example of an app taking advantage of this feature is a media player or a video chat app. This snippet allows you to switch into compact overlay mode or return to the default mode.", function.Description, "Description didn't parse.");

            Assert.IsNotNull(function.Parameters);
            Assert.AreEqual(1, function.Parameters.Count, "Parameters didn't parse.");
            Assert.AreEqual("Force Compact Overlay mode.", function.Parameters.First().Description, "Parameter didn't parse properly.");

            Assert.IsNotNull(function.Returns);
            Assert.AreEqual("Promise with new mode value (1=CompactOverlay | 0=Default).", function.Returns.Description, "Return didn't parse.");
        }

        [TestMethod]
        public void TestDescriptionOverride()
        {
            JSDocParser parser = new JSDocParser();

            var parse = parser.Parse("TestFiles\\descOverrideSummary.js", true);

            Assert.IsNotNull(parse);

            Assert.IsNull(parse.File);
            Assert.IsNotNull(parse.Functions);

            Assert.AreEqual(1, parse.Functions.Count, "Expected Function Overview");

            var function = parse.Functions.First();

            Assert.AreEqual("This overrides the description.", function.Description, "Description wasn't overwritten.");        
        }

        [TestMethod]
        public void TestSummary()
        {
            JSDocParser parser = new JSDocParser();

            var parse = parser.Parse("TestFiles\\descOverrideSummary.js", true);

            Assert.IsNotNull(parse);

            Assert.IsNull(parse.File);
            Assert.IsNotNull(parse.Functions);

            Assert.AreEqual(1, parse.Functions.Count, "Expected Function Overview");

            var function = parse.Functions.First();

            Assert.AreEqual("This is a short summary.", function.Summary, "Summary wasn't parsed.");
        }
    }
}
