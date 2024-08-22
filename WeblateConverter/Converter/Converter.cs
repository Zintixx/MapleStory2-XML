using System.Xml;
using Newtonsoft.Json.Linq;
using WeblateConverter.Enum;

namespace WeblateConverter.Converter;

public class Converter {
    public void Select() {
        Console.WriteLine("Select 1 to convert XML to JSON");
        Console.WriteLine("Select 2 to convert JSON to XML");
        string? input = Console.ReadLine();

        if (input == "1") {
            XmlToJson();
        } else if (input == "2") {
            Console.WriteLine("Not implemented");
        } else {
            Console.WriteLine("Invalid input");
            return;
        }
    }
    private void XmlToJson() {
        Console.WriteLine("Enter the locale you want to convert to JSON");
        string? locale = Console.ReadLine();

        // get all enum names in Locale
        string[] enumNames = System.Enum.GetNames(typeof(Locale));

        // Check if locale1 is a valid enum
        while (System.Enum.TryParse<Locale>(locale, out _) == false) {
            Console.WriteLine($"Invalid locale, valid locales are: {string.Join(", ", enumNames)}");
            locale = Console.ReadLine();
        }

        if (ProcessLocaleStrings(locale)) {
            Console.WriteLine("Conversion successful");
        } else {
            Console.WriteLine("Conversion failed");
        }

        // return to Select if user wants to, otherwise exit
        Console.WriteLine("Do you want to convert another locale? (y/n)");
        string? input = Console.ReadLine();
        if (input == "y") {
            Select();
        } else {
            return;
        }
    }

    private bool ProcessLocaleStrings(string locale) {
        FileInfo[] enStrings = GetFiles(locale);

        if (enStrings.Length == 0) {
            Console.WriteLine($"No {locale} files found");
            return false;
        }

        // Parse all enStrings to XML
        Dictionary<string, XmlDocument> enXmls = new Dictionary<string, XmlDocument>();
        foreach (FileInfo file in enStrings) {
            XmlDocument xml = new XmlDocument();
            xml.Load(file.FullName);
            enXmls.Add(file.Name, xml);
        }


        var questJsons = new List<string>();
        var skillJsons = new List<string>();
        var skillDescriptions = new List<string>();
        var skillAdditionalDescriptions = new List<string>();
        foreach ((string name, XmlDocument xml) in enXmls) {
            Console.WriteLine($"Parsing {name}");
            string? json = Parse(xml);
            if (json == null) {
                Console.WriteLine($"Failed to parse {name}");
                continue;
            }

            if (locale == "en") {
                switch (name)
                {
                    case not null when name.StartsWith("questdescription"):
                        questJsons.Add(json);
                        continue;
                    case not null when name.StartsWith("skillname"):
                        skillJsons.Add(json);
                        continue;
                    case not null when name.StartsWith("korskilldescription"):
                        skillDescriptions.Add(json);
                        continue;
                    case not null when name.StartsWith("koradditionaldescription"):
                        skillAdditionalDescriptions.Add(json);
                        continue;
                }
            }


            SaveToJson(name, json, locale);
        }

        if (locale == "en") {
            SaveToJson("questdescription_final.xml", CombineJsons(questJsons), locale);
            SaveToJson("skillname.xml", CombineJsons(skillJsons), locale);
            SaveToJson("stringskilldescription.xml", CombineJsons(skillDescriptions), locale);
            SaveToJson("stringadditionaldescription.xml", CombineJsons(skillAdditionalDescriptions), locale);
        }
        return true;
    }

    private string CombineJsons(List<string> jsons) {
        var jsonObject = new JObject();
        foreach (string json in jsons) {
            JObject jObject = JObject.Parse(json);
            foreach (var (key, value) in jObject) {
                jsonObject[key] = value;
            }
        }
        return jsonObject.ToString();
    }

    public bool SaveToJson(string name, string json, string locale) {
        string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Json", locale, name.Replace(".xml", ".json"));
        string? directoryPath = Path.GetDirectoryName(jsonPath);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Json", locale));
        }

        File.WriteAllText(jsonPath, json);
        return true;
    }

    private FileInfo[] GetFiles(string locale) {
        // Get the base directory of the application
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Navigate up to the project root directory
        string? projectRoot = Directory.GetParent(baseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName;
        if (projectRoot == null) {
            Console.WriteLine("Project root not found");
            return [];
        }

        // Combine the project root with the relative path to the "Xml" folder
        string xmlPath = Path.Combine(projectRoot, "Xml");

        Console.WriteLine($"Reading XML files from {xmlPath}");

        // Get directory information
        var directoryInfo = new DirectoryInfo(xmlPath);

        // Output directory information
        Console.WriteLine($"Directory: {directoryInfo.FullName}");

        // get specific directory name in directoryInfo
        DirectoryInfo? stringDirectory = directoryInfo.GetDirectories("string").FirstOrDefault();

        if (stringDirectory == null) {
            Console.WriteLine("String directory not found");
            return [];
        }

        // Get en directory information
        DirectoryInfo? localeDirectory = stringDirectory.GetDirectories(locale).FirstOrDefault();

        if (localeDirectory == null) {
            Console.WriteLine($"{locale} directory not found");
            return [];
        }

        // Get all files in the en directory
        FileInfo[] files = localeDirectory.GetFiles();
        return files;
    }

    public string? Parse(XmlDocument document) {
        XmlNode? node = document.SelectSingleNode("ms2");
        if (node == null) {
            Console.WriteLine("ms2 node not found");
            return null;
        }

        if (node.ChildNodes.Count == 0) {
            Console.WriteLine($"No child nodes found in {document.BaseURI}");
            return null;
        }
        var jsonObject = new JObject();
        foreach (XmlNode childNode in node.ChildNodes) {
            if (childNode.Attributes?.Count == 0) {
                Console.WriteLine($"No attributes found in node in {document.BaseURI}");
                continue;
            }
            // get first attribute
            XmlAttribute keyAttribute = childNode.Attributes![0];
            string key = keyAttribute.Value;

            var attributesObject = new JObject();
            string feature = "null";
            string locale = "null";
            // get each attribute, skipping the first
            for (int i = 1; i < childNode.Attributes.Count; i++) {
                XmlAttribute attribute = childNode.Attributes[i];
                if (attribute == null) {
                    Console.WriteLine("Attribute not found");
                    continue;
                }

                if (attribute.Name == "feature") {
                    feature = attribute.Value;
                    continue;
                }
                if (attribute.Name == "locale") {
                    locale = attribute.Value;
                    continue;
                }

                attributesObject[attribute.Name] = attribute.Value;

                jsonObject[key+"-"+feature+"-"+locale] = attributesObject;
            }

            jsonObject[key+"-"+feature+"-"+locale] = attributesObject;

        }

        return jsonObject.ToString();
    }
}
