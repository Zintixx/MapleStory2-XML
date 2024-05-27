using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using GMS2.To.KMS2;
using Maple2.File.Parser.Tools;

string clientTableFolder = Path.Combine(Paths.GMS2_DIR, "table");
string[] clientXmlFiles = Directory.GetFiles(clientTableFolder, "individualitemdrop*.xml", SearchOption.AllDirectories);
// remove individualitemdrop_monster
clientXmlFiles = clientXmlFiles.Where(x => !x.Contains("monster")).ToArray();

List<Maple2.File.Parser.Xml.Table.IndividualItemDropRoot> individualItemDropRoots = [];
foreach (string xmlPath in clientXmlFiles) {
    Maple2.File.Parser.Xml.Table.IndividualItemDropRoot? clientData = DeserializeXml<Maple2.File.Parser.Xml.Table.IndividualItemDropRoot>(xmlPath);
    if (clientData == null) {
        throw new Exception("Failed to deserialize xml");
    }
    individualItemDropRoots.Add(clientData);
}

IEnumerable<KeyValuePair<GroupKey, List<Maple2.File.Parser.Xml.Table.IndividualItemDrop>>> clientIndividualDropBoxesGrouped = individualItemDropRoots
    .SelectMany(data => data.__individualDropBox)
    // .Where(dropbox => dropbox.Locale == "NA" || dropbox.Locale == "")
    .GroupBy(dropbox => new GroupKey(dropbox.individualDropBoxID, dropbox.dropGroup))
    .ToDictionary(group => group.Key, group => group.ToList())
    .Where(dict => dict.Value.Count > 0);

List<Maple2.File.Parser.Xml.Table.Server.IndividualItemDrop> clientIndividualDropBoxes = ConvertToServerIndividualItemDrop(clientIndividualDropBoxesGrouped).ToList();

// count dropBoxes
int dropBoxCount = clientIndividualDropBoxes.GroupBy(x => x.dropBoxID).Count();

string serverXmlFile = Path.Combine(Paths.SERVER_DIR, "table", "server", "individualItemDrop_Final_Backup.xml");

Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot? serverData = DeserializeXml<Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot>(serverXmlFile);
if (serverData == null) {
    throw new Exception("Failed to deserialize xml");
}

List<Maple2.File.Parser.Xml.Table.Server.IndividualItemDrop> serverIndividualDropBoxes = serverData.dropBox;

// merge client and server data
foreach (var clientDropBox in clientIndividualDropBoxes) {
    var serverDropBox = serverIndividualDropBoxes.FirstOrDefault(x => x.dropBoxID == clientDropBox.dropBoxID);
    if (serverDropBox == null) {
        serverIndividualDropBoxes.Add(clientDropBox);
        continue;
    }

    var clientGroup = clientDropBox.__group.First();
    var serverGroup = serverDropBox.__group.FirstOrDefault(x => x.dropGroupID == clientGroup.dropGroupID);
    if (serverGroup == null) {
        serverDropBox.__group.Add(clientGroup);
        continue;
    }

    // since group exists, make all locale KR
    serverGroup._locale = "KR";
    foreach (var item in serverGroup.__v) {
        item._locale = "KR";
    }

    foreach (var clientItem in clientGroup.__v) {
        var serverItem = serverGroup.__v.FirstOrDefault(x => x.itemID == clientItem.itemID && (x.Locale == clientItem.Locale || x.Locale == ""));
        if (serverItem == null) {

            serverGroup.__v.Add(clientItem);
            continue;
        }

        serverItem._locale = clientItem._locale;
        continue;
    }

    // if group has multiple items with different locales, set the group locale to empty
    if (serverGroup.__v.Select(x => x._locale).Distinct().Count() > 1) {
        serverGroup._locale = "";
    }
}

// order by dropBoxID, dropGroupID, itemID
serverIndividualDropBoxes = [.. serverIndividualDropBoxes.OrderBy(x => x.dropBoxID)];

foreach (var dropBox in serverIndividualDropBoxes) {
    dropBox.__group = [.. dropBox.__group.OrderBy(x => x.dropGroupID)];
    foreach (var group in dropBox.__group) {
        group.__v = [.. group.__v.OrderBy(x => x.itemID)];
    }
}

// //split serverIndividualDropBoxes into groups of 1000
// int groupSize = 1000;
// int groupCount = (int) Math.Ceiling((double) serverIndividualDropBoxes.Count / groupSize);

// for (int i = 0; i < groupCount; i++) {
//     int start = i * groupSize;
//     int end = Math.Min((i + 1) * groupSize, serverIndividualDropBoxes.Count);
//     var group = serverIndividualDropBoxes.GetRange(start, end - start);

//     Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot individualItemDropRoot = new Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot() {
//         dropBox = group
//     };

//     XmlSerializer groupsSerializer = new XmlSerializer(typeof(Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot));
//     string outputXmlPath = Path.Combine(Paths.SOLUTION_DIR, "individualItemDrop_Final_" + i + ".xml");

//     using XmlWriter writer = XmlWriter.Create(outputXmlPath, new XmlWriterSettings { Indent = true });
//     groupsSerializer.Serialize(writer, individualItemDropRoot);

//     Console.WriteLine("Groups saved to file: " + outputXmlPath);
// }

Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot individualItemDropRoot = new Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot() {
    dropBox = serverIndividualDropBoxes
};

XmlSerializer groupsSerializer = new XmlSerializer(typeof(Maple2.File.Parser.Xml.Table.Server.IndividualItemDropRoot));
string outputXmlPath = Path.Combine(Paths.SOLUTION_DIR, "server", "table", "server", "individualItemDrop_Final.xml");
using XmlWriter writer = XmlWriter.Create(outputXmlPath, new XmlWriterSettings { Indent = true });
groupsSerializer.Serialize(writer, individualItemDropRoot);
writer.Close();

Console.WriteLine("Saved to file: " + outputXmlPath);

// Cleanup XML
string xmlString = File.ReadAllText(outputXmlPath);
xmlString = Regex.Replace(xmlString, " \\w+=\"\"", string.Empty); // remove empty attributes
// minCount="1" maxCount="1"
xmlString = Regex.Replace(xmlString, " minCount=\"1\" maxCount=\"1\"", string.Empty);
File.WriteAllText(outputXmlPath, xmlString);


#region Methods

static T? DeserializeXml<T>(string xmlPath) where T : class {
    string xmlString = File.ReadAllText(xmlPath);
    xmlString = Sanitizer.RemoveEmpty(xmlString);
    xmlString = Sanitizer.SanitizeBool(xmlString);

    XmlReader xml = XmlReader.Create(new StringReader(xmlString));
    XmlSerializer individualItemDropSerializer = new XmlSerializer(typeof(T));
    T? data = individualItemDropSerializer.Deserialize(xml) as T;
    return data;
}

IEnumerable<Maple2.File.Parser.Xml.Table.Server.IndividualItemDrop> ConvertToServerIndividualItemDrop(IEnumerable<KeyValuePair<GroupKey, List<Maple2.File.Parser.Xml.Table.IndividualItemDrop>>> clientIndividualDropBoxesGrouped) {
    foreach (var dropbox in clientIndividualDropBoxesGrouped) {
        var serverDropBox = new Maple2.File.Parser.Xml.Table.Server.IndividualItemDrop() {
            dropBoxID = dropbox.Key.IndividualDropBoxID,
            __group = [
                new() {
                    dropGroupID = dropbox.Key.DropGroup,
                    smartDropRate = dropbox.Value.First().smartDropRate,
                    dropGroupMinLevel = dropbox.Value.First().dropGroupMinLevel,
                    dropCount = dropbox.Value.First().dropCount,
                    dropCountProbability = dropbox.Value.First().dropCountProbability,
                    serverDrop = dropbox.Value.First().serverDrop,
                    isApplySmartGenderDrop = dropbox.Value.First().isApplySmartGenderDrop,
                    __v = dropbox.Value.Select(x => new Maple2.File.Parser.Xml.Table.Server.IndividualItemDrop.Group.Item() {
                        itemID = x.item,
                        itemID2 = x.item2,
                        isAnnounce = x.isAnnounce,
                        properJobWeight = x.properJobWeight,
                        imProperJobWeight = x.imProperJobWeight,
                        weight = x.weight,
                        assistBonus = x.assistBonus,
                        uiItemRank = (short) (x.PackageUIShowGrade ?? 0),
                        gradeProbability = x.gradeProbability,
                        grade = x.grade.Length > 0 ? x.grade.ToList().Select(y => (short) y).ToArray() : x.PackageUIShowGrade == null ? [] : [(short) x.PackageUIShowGrade],
                        enchantLevel = x.enchantLevel,
                        socketDataID = x.socketDataID,
                        tradableCountDeduction = x.tradableCountDeduction,
                        rePackingLimitCountDeduction = x.rePackingLimitCountDeduction,
                        isBindCharacter = x.isBindCharacter,
                        showTooltip = x.showTooltip,
                        disableBreak = x.disableBreak,
                        showIconOnGachaUI = x.showIconOnGachaUI,
                        mapDependency = x.mapDependency,
                        constraintsQuest = x.constraintsQuest,
                        minCount = (int) x.minCount,
                        maxCount = (int) x.maxCount,
                        _locale = x.Locale,
                    }).ToList()
                }
            ]
        };

        yield return serverDropBox;
    }
}

internal record GroupKey(int IndividualDropBoxID, byte DropGroup);

#endregion
