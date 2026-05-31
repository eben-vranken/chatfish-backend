#!/usr/bin/env node

const { MongoClient, ObjectId } = require("mongodb");
const bcrypt = require("bcrypt");
const crypto = require("crypto");
const https = require("https");
const http = require("http");
const { Client } = require("minio");

const DEFAULT_CONNECTION_STRING =
  process.argv[2] || process.env.MONGODB_CONNECTION_STRING || "mongodb://localhost:27017";
const DEFAULT_DATABASE_NAME =
  process.argv[3] || process.env.MONGODB_DATABASE_NAME || "ChatfishDbDemo";
const DEFAULT_MINIO_ENDPOINT = process.argv[4] || process.env.MINIO_ENDPOINT || "localhost:9000";
const DEFAULT_MINIO_ACCESS_KEY = process.argv[5] || process.env.MINIO_AK || "minioadmin";
const DEFAULT_MINIO_SECRET_KEY = process.argv[6] || process.env.MINIO_SK || "minioadmin";
const DEFAULT_MINIO_USE_SSL =
  process.env.MINIO_USE_SSL === "true" ||
  DEFAULT_MINIO_ENDPOINT.startsWith("https://");

const BUCKET_CHARACTERS = "characters";
const BUCKET_CHATS = "chats";
const BUCKET_STORYMESSAGES = "storymessages";

function parseMinioEndpoint(endpoint) {
  if (endpoint.startsWith("http://") || endpoint.startsWith("https://")) {
    const url = new URL(endpoint);
    return {
      host: url.hostname,
      port: url.port ? parseInt(url.port) : url.protocol === "https:" ? 443 : 80,
      useSSL: url.protocol === "https:",
    };
  }
  const [host, port] = endpoint.split(":");
  return { host, port: port ? parseInt(port) : 9000, useSSL: DEFAULT_MINIO_USE_SSL };
}

const minioEndpoint = parseMinioEndpoint(DEFAULT_MINIO_ENDPOINT);
const minioClient = new Client({
  endPoint: minioEndpoint.host,
  port: minioEndpoint.port,
  useSSL: minioEndpoint.useSSL,
  accessKey: DEFAULT_MINIO_ACCESS_KEY,
  secretKey: DEFAULT_MINIO_SECRET_KEY,
});

function calculateSHA256(buffer) {
  return crypto.createHash("sha256").update(buffer).digest("hex").toLowerCase();
}

function downloadFile(url, timeout = 15000) {
  return new Promise((resolve, reject) => {
    const protocol = url.startsWith("https") ? https : http;
    const req = protocol.get(url, (res) => {
      if (res.statusCode !== 200) return reject(new Error(`Failed to download: ${res.statusCode}`));
      const chunks = [];
      res.on("data", (c) => chunks.push(c));
      res.on("end", () => resolve(Buffer.concat(chunks)));
    });
    req.on("error", reject);
    req.setTimeout(timeout, () => {
      req.destroy();
      reject(new Error(`Timeout after ${timeout}ms`));
    });
  });
}

async function uploadImageFromUrl(bucketName, url) {
  try {
    const buffer = await downloadFile(url);
    const hash = calculateSHA256(buffer);
    if (!(await minioClient.bucketExists(bucketName))) await minioClient.makeBucket(bucketName);
    await minioClient.putObject(bucketName, hash, buffer, buffer.length, { "Content-Type": "image/jpeg" });
    return hash;
  } catch (e) {
    return "";
  }
}

async function uploadRandomImage(bucketName = BUCKET_CHARACTERS) {
  try {
    const userData = await new Promise((resolve, reject) => {
      const req = https.get("https://randomuser.me/api/?inc=picture&noinfo", (res) => {
        let data = "";
        res.on("data", (c) => (data += c));
        res.on("end", () => resolve(JSON.parse(data)));
      });
      req.on("error", reject);
      req.setTimeout(10000, () => {
        req.destroy();
        reject(new Error("RandomUser API timeout"));
      });
    });
    const imageUrl = userData.results[0].picture.large;
    return await uploadImageFromUrl(bucketName, imageUrl);
  } catch {
    return "";
  }
}

function utcDate(year, month1Based, day, hour = 8, minute = 0) {
  return new Date(Date.UTC(year, month1Based - 1, day, hour, minute, 0));
}

function distributeDates(totalMessages, startDate, endDate, climaxDate, climaxFraction = 0.6) {
  const days = [];
  for (let d = new Date(startDate); d <= endDate; d.setUTCDate(d.getUTCDate() + 1)) {
    const copy = new Date(d.valueOf());
    days.push(copy);
  }
  const climaxStr = new Date(climaxDate).toISOString().slice(0, 10);
  const otherDays = days.filter((d) => d.toISOString().slice(0, 10) !== climaxStr);
  const assignment = [];
  const climaxCount = Math.max(1, Math.floor(totalMessages * climaxFraction));
  let remaining = totalMessages - climaxCount;
  // assign 1-2 messages per other day until remaining exhausted
  let idx = 0;
  while (remaining > 0 && otherDays.length > 0) {
    const take = Math.min(remaining, Math.random() < 0.3 ? 2 : 1);
    // push `take` messages for this day
    for (let t = 0; t < take; t++) assignment.push(new Date(otherDays[idx].valueOf()));
    remaining -= take;
    idx = (idx + 1) % otherDays.length;
    // if we've looped many times and still have remaining, allow extra per day
  }
  // if still remaining (few days only), append to climax pool
  if (remaining > 0) {
    // add them to climaxCount logically by increasing climaxCount variable later
  }
  // now create climax timestamps spread through the day starting at 08:00 every few minutes
  for (let i = 0; i < climaxCount; i++) {
    const t = new Date(climaxDate.valueOf());
    t.setUTCMinutes(t.getUTCMinutes() + i * 10);
    assignment.push(t);
  }
  // if we didn't reach totalMessages, append additional timestamps after climax
  while (assignment.length < totalMessages) {
    const extra = new Date(endDate.valueOf());
    extra.setUTCHours(extra.getUTCHours() + (assignment.length - totalMessages + 1));
    assignment.push(extra);
  }
  // sort assignment
  assignment.sort((a, b) => a - b);
  return assignment.slice(0, totalMessages);
}

async function seedDatabase() {
  const client = new MongoClient(DEFAULT_CONNECTION_STRING);
  try {
    await client.connect();
    const db = client.db(DEFAULT_DATABASE_NAME);

    // Ensure all MinIO buckets exist
    const buckets = [BUCKET_CHARACTERS, BUCKET_CHATS, BUCKET_STORYMESSAGES];
    for (const bucket of buckets) {
      if (!(await minioClient.bucketExists(bucket))) {
        await minioClient.makeBucket(bucket);
        console.log(`✅ Created bucket: ${bucket}`);
      }
    }

    const collections = [
      "Users",
      "Scenarios",
      "Channels",
      "Characters",
      "Chats",
      "Posts",
      "Comments",
      "StoryMessages",
      "Tickets",
    ];
    for (const c of collections) await db.collection(c).deleteMany({});

    const hashedAdminPassword = await bcrypt.hash("admin123", 10);
    const hashedPassword = await bcrypt.hash("wachtwoord123", 10);
    const users = [
      { _id: new ObjectId(), Username: "admin", Email: "admin@chatfish.be", Password: hashedAdminPassword, Role: "admin" },
      { _id: new ObjectId(), Username: "jan.jansen", Email: "jan.jansen@example.com", Password: hashedPassword, Role: "user" },
      { _id: new ObjectId(), Username: "maria.devries", Email: "maria.devries@example.com", Password: hashedPassword, Role: "user" },
    ];
    await db.collection("Users").insertMany(users);

    const scenarioAId = new ObjectId();
    const scenarioBId = new ObjectId();
    const scenarioCId = new ObjectId();

    const scenarios = [
      { _id: scenarioAId, Name: "Verdwenen Student", Description: "Mysterie rond een verdwenen student", CreatedBy: users[0]._id, StartMoment: new Date("2026-06-15T20:00:00Z"), DurationMinutes: 90, Price: 12.50, SaleStatus: "open" },
      { _id: scenarioBId, Name: "Blackout", Description: "Een stad zonder stroom en sabotage", CreatedBy: users[0]._id, StartMoment: new Date("2026-07-20T19:30:00Z"), DurationMinutes: 120, Price: 15.00, SaleStatus: "gesloten" },
      // Scenario C: startmoment in het verleden, zodat functionaliteit die afhankelijk is van een
      // reeds gestart stuk (tijdlijn, automatische navigatie) getest kan worden.
      { _id: scenarioCId, Name: "De Nacht van de Waarheid", Description: "Een groep vrienden ontdekt een schokkend geheim tijdens een reünie", CreatedBy: users[0]._id, StartMoment: new Date("2026-05-20T20:00:00Z"), DurationMinutes: 90, Price: 12.50, SaleStatus: "open" },
    ];
    await db.collection("Scenarios").insertMany(scenarios);

    const namesA = ["Anna Peeters", "Bram Claes", "Celine Jacobs", "David Maes"];
    const namesB = ["Eva Van den Berg", "Frank De Smet", "Gina Verhoeven", "Hugo Vandenbosch"];
    const namesC = ["Lena Claes", "Niels Bogaert", "Sara Hermans", "Thomas Willems"];
    const charsA = [];
    const charsB = [];
    const charsC = [];
    for (let i = 0; i < 4; i++) {
      const hashA = await uploadRandomImage(BUCKET_CHARACTERS);
      const hashB = await uploadRandomImage(BUCKET_CHARACTERS);
      const hashC = await uploadRandomImage(BUCKET_CHARACTERS);
      charsA.push({ _id: new ObjectId(), Name: namesA[i], ScenarioId: scenarioAId, ProfilePicture: hashA });
      charsB.push({ _id: new ObjectId(), Name: namesB[i], ScenarioId: scenarioBId, ProfilePicture: hashB });
      charsC.push({ _id: new ObjectId(), Name: namesC[i], ScenarioId: scenarioCId, ProfilePicture: hashC });
    }
    await db.collection("Characters").insertMany([...charsA, ...charsB, ...charsC]);

    const channelsA = [new ObjectId(), new ObjectId()];
    const channelsB = [new ObjectId(), new ObjectId()];
    const channelsC = [new ObjectId(), new ObjectId()];

    const channelsToInsert = [
      { _id: channelsA[0], ChannelName: "Algemeen A", ChannelDescription: "Hoofd channel scenario A", ScenarioId: scenarioAId },
      { _id: channelsA[1], ChannelName: "Onderzoek A", ChannelDescription: "Discussie en updates A", ScenarioId: scenarioAId },
      { _id: channelsB[0], ChannelName: "Algemeen B", ChannelDescription: "Hoofd channel scenario B", ScenarioId: scenarioBId },
      { _id: channelsB[1], ChannelName: "Crisis B", ChannelDescription: "Crisis updates B", ScenarioId: scenarioBId },
      { _id: channelsC[0], ChannelName: "Algemeen C", ChannelDescription: "Hoofd channel scenario C", ScenarioId: scenarioCId },
      { _id: channelsC[1], ChannelName: "Geheimen C", ChannelDescription: "Discussies over het geheim", ScenarioId: scenarioCId },
    ];
    await db.collection("Channels").insertMany(channelsToInsert);

    const chatsA = { main: new ObjectId(), side1: new ObjectId(), side2: new ObjectId() };
    const chatsB = { main: new ObjectId(), side1: new ObjectId(), side2: new ObjectId() };
    const chatsC = { main: new ObjectId(), side1: new ObjectId(), side2: new ObjectId() };

    const chatPicA1 = await uploadRandomImage(BUCKET_CHATS);
    const chatPicA2 = await uploadRandomImage(BUCKET_CHATS);
    const chatPicB1 = await uploadRandomImage(BUCKET_CHATS);
    const chatPicB2 = await uploadRandomImage(BUCKET_CHATS);
    const chatPicC1 = await uploadRandomImage(BUCKET_CHATS);
    const chatPicC2 = await uploadRandomImage(BUCKET_CHATS);

    const chatsToInsert = [
      { _id: chatsA.main, Name: "Groepschat Verdwijning", ScenarioId: scenarioAId, ProfilePicture: chatPicA1 },
      { _id: chatsA.side1, Name: "Privé: Anna & Bram", ScenarioId: scenarioAId, ProfilePicture: chatPicA2 },
      { _id: chatsA.side2, Name: "Privé: Celine & David", ScenarioId: scenarioAId, ProfilePicture: chatPicA2 },
      { _id: chatsB.main, Name: "Groepschat Blackout", ScenarioId: scenarioBId, ProfilePicture: chatPicB1 },
      { _id: chatsB.side1, Name: "Privé: Eva & Frank", ScenarioId: scenarioBId, ProfilePicture: chatPicB2 },
      { _id: chatsB.side2, Name: "Privé: Gina & Hugo", ScenarioId: scenarioBId, ProfilePicture: chatPicB2 },
      { _id: chatsC.main, Name: "Groepschat Reünie", ScenarioId: scenarioCId, ProfilePicture: chatPicC1 },
      { _id: chatsC.side1, Name: "Privé: Lena & Niels", ScenarioId: scenarioCId, ProfilePicture: chatPicC2 },
      { _id: chatsC.side2, Name: "Privé: Sara & Thomas", ScenarioId: scenarioCId, ProfilePicture: chatPicC2 },
    ];
    await db.collection("Chats").insertMany(chatsToInsert);

    const start = utcDate(2026, 1, 2, 8, 0);
    const end = utcDate(2026, 1, 10, 20, 0);
    const climax = utcDate(2026, 1, 9, 9, 0);

    // Scenario C: tijdlijn loopt van 10 t/m 20 mei 2026 — alles in het verleden.
    const startC = utcDate(2026, 5, 10, 8, 0);
    const endC   = utcDate(2026, 5, 20, 20, 0);
    const climaxC = utcDate(2026, 5, 20, 9, 0);

    const mainTextsA = [
      "Een student komt niet opdagen na een feestje en vrienden maken zich zorgen.",
      "Foto's van die avond circuleren en er is onduidelijkheid over de route.",
      "De vrienden starten een groepschat om details te verzamelen.",
      "Een aanwijzing zegt dat de student richting het park liep.",
      "Een bruine rugzak wordt gevonden bij de uitgang van het park.",
      "De politie vraagt om bewijsmateriaal en foto's.",
      "Iemand deelt een verdacht kenteken op de chat.",
      "Er ontstaat verdeeldheid over wie welke info had.",
      "Een getuige meldt een ziening bij een station.",
      "De vrienden organiseren metropunten om tips te verzamelen.",
      "Er worden CCTV-beelden gedeeld door een vrijwilliger.",
      "Iemand herkent een figuur op de beelden vooraan in de avond.",
      "De groep besluit in shifts te zoeken de volgende ochtend.",
      "De politie sluit sommige gebieden af voor onderzoek.",
      "Een nieuwe tip leidt naar een oud garagepand.",
      "Er wordt een plan gemaakt om het pand veilig te betreden.",
      "Sommigen zijn bang voor confrontatie met mogelijke verdachten.",
      "Er circuleren contradictorische aanwijzingen op social media.",
      "Een vrijwilliger meldt dat er voetafdrukken zijn gevonden.",
      "De spanning stijgt na een verontrustende voicemail.",
      "De vrienden proberen structuur te brengen in hun zoektocht.",
      "Er wordt een lijst gemaakt met mogelijke ontmoetingspunten.",
      "Een anonieme tipgever claimt iets gezien te hebben in de schemering.",
      "De groep plant een gecoördineerde zoektocht in teams.",
      "Een team rapporteert een leeg huis met recente activiteit.",
      "De politie begint forensisch materiaal te verzamelen.",
      "Er wordt gefocust op het achterhalen van de route van de student.",
      "Familieleden worden op de hoogte gebracht en komen bij elkaar.",
      "De chat wisselt tussen hoop en paniek.",
      "Iemand onthult een conflict tussen de student en een bekende.",
      "De groep herbekijkt berichten van de avond en zoekt inconsistenties.",
      "Een klein spoor leidt naar een oude schuur buiten de stad.",
      "De groep waarschuwt elkaar voor mogelijke gevaarlijke locaties.",
      "Er wordt een plan gemaakt om professionele hulp in te schakelen.",
      "Een onverwachte getuige meldt zich met nieuw beeldmateriaal.",
      "De politie verhoort meerdere betrokkenen.",
      "Een verdachte wordt tijdelijk vastgehouden voor verhoor.",
      "De chat toont opluchting bij elk klein nieuwtje.",
      "Er wordt gezocht naar een logische verklaring voor de verdwijning.",
      "De vrienden verzamelen bewijs en delen het met de politie.",
      "Een sleutelvoorwerp wordt geïdentificeerd als relevant.",
      "De groep plant een finale zoektocht bij het oude spoor.",
      "De spanning bereikt een hoogtepunt wanneer iedereen convergeert.",
      "Op de climaxdag blijken verschillende aanwijzingen samen te komen.",
      "Een laatste tip leidt naar een veilige vondst bij een oud gebouw.",
      "De student wordt gevonden en er is een emotionele ontlading.",
      "Vrienden delen foto's van de hereniging en bedanken iedereen.",
    ];

    const mainTextsB = [
      "Een grote stroomuitval treft delen van de stad in de vroege uren.",
      "Het crisisteam komt bijeen en deelt updates in de groepschat.",
      "Inwoners gebruiken batterijen en kaarsen om de situatie te overleven.",
      "Er worden geruchten verspreid over sabotage aan het netwerk.",
      "Een pompstation meldt problemen en ziekenhuizen schakelen over op generators.",
      "Vrijwilligers organiseren telefoonketens en ondersteuningspunten.",
      "Er duiken berichten op van mensen die hun huis niet kunnen verlaten.",
      "Het openbaar vervoer werkt onregelmatig en lijnen vallen uit.",
      "De burgemeester publiceert een korte verklaring over de situatie.",
      "Een technisch team deelt updates over herstelpogingen.",
      "Bewoners delen foto's van donkere straten en vastgelopen treinen.",
      "Er ontstaan discussies over de prioriteit van hulpverlening.",
      "Lokale ondernemers organiseren oplaadpunten voor telefoons.",
      "Het crisisteam onderzoekt mogelijke sabotage en menselijke fouten.",
      "Er worden checkpoints geplaatst voor essentiële hulpgoederen.",
      "Sommige bewoners bieden warme maaltijden aan voor getroffen gezinnen.",
      "Er is oplopende frustratie, maar ook veel onderlinge hulp.",
      "De communicatie verbetert dankzij tijdelijke radioverbindingen.",
      "Een nieuw alarmbeeld geeft aanwijzingen over een verdachte locatie.",
      "Teams coördineren herstelinspanningen op kwetsbare locaties.",
      "De chat geeft praktische instructies voor veilig gedrag.",
      "Er wordt een plan opgesteld om kritieke infrastruktuur te beschermen.",
      "Vrijwilligers rapporteren successen en tegenslagen.",
      "Een generator faalt tijdelijk en veroorzaakt zorgen in een wijk.",
      "Het crisisteam evalueert mogelijke lange-termijnmaatregelen.",
      "De stad leert van fouten en past procedures aan.",
      "Er worden positieve verhalen gedeeld over samenwerking.",
      "De media besteden aandacht aan lokale helden.",
      "De chat documenteert de weg naar herstel.",
      "Er ontstaat hoop wanneer een belangrijk knooppunt gerepareerd wordt.",
      "De gemeenschap viert kleine successen tijdens de week.",
      "Op de climaxdag blijken alle inspanningen samen te komen.",
      "Een cruciale reparatie maakt het netwerk weer stabiel.",
      "Bewoners delen foto's van verlichte straten en opgeluchte gezichten.",
      "Het crisisteam reflecteert op wat er geleerd is.",
      "Er wordt gewerkt aan preventie voor de toekomst.",
      "De stad organiseert een dankmoment voor hulpverleners.",
      "Er ontstaat een plan om de infrastructuur te versterken.",
      "De chat sluit af met een gevoel van herstel en samenzijn.",
    ];

    function makeSideTexts(mainTexts, pairNames, total) {
      const out = [];
      for (let i = 0; i < total; i++) {
        const ref = mainTexts[i % mainTexts.length];
        out.push(ref);
      }
      return out;
    }

    const mainTextsC = [
      "De reünie begint en iedereen doet alsof er niets aan de hand is.",
      "Lena merkt dat Thomas haar vermijdt, al van bij aankomst.",
      "Er wordt een oude foto gevonden die vragen oproept.",
      "Niels begint te praten over 'die nacht' maar stopt abrupt.",
      "Sara trekt Lena apart en fluistert iets onrustwekkends.",
      "Een onbekend nummer stuurt een berichtje naar de groep.",
      "Thomas ontkent alles maar zijn gezicht vertelt iets anders.",
      "De wijn vloeit en de tongen beginnen los te komen.",
      "Een schreeuw vanuit de tuin trekt ieders aandacht.",
      "Het blijkt een valse alarm, maar de spanning is voelbaar.",
      "Niels vindt iets in de kelder dat hij niet had verwacht.",
      "Sara weigert te vertellen wat ze weet totdat iedereen aanwezig is.",
      "Lena ontdekt een dagboek dat zeven jaar verborgen was.",
      "De groep verzamelt zich in de woonkamer voor een confrontatie.",
      "Thomas geeft eindelijk toe dat hij er die nacht bij was.",
      "Een naam valt die niemand had verwacht te horen.",
      "De vriendschappen komen onder zware druk te staan.",
      "Niels breekt in tranen uit en vraagt om vergiffenis.",
      "Sara onthult dat ze het geheim al jaren bewaart.",
      "De waarheid komt langzaam maar zeker naar boven.",
      "Iemand dreigt de kamer te verlaten als het gesprek doorgaat.",
      "Lena stelt voor om samen een beslissing te nemen.",
      "De groep weegt de consequenties van de waarheid af.",
      "Thomas stuurt een bericht naar iemand buiten de groep.",
      "Een auto stopt voor het huis — onverwacht bezoek.",
      "Het bezoek brengt nieuwe informatie die alles verandert.",
      "De groep beseft dat ze niet langer kunnen zwijgen.",
      "Een gezamenlijk besluit wordt genomen na uren van debat.",
      "De nacht eindigt met tranen, opluchting en gebroken stiltes.",
      "Iedereen vertrekt met het gevoel dat niets meer hetzelfde zal zijn.",
    ];

    const sideA1Texts = makeSideTexts(mainTextsA, ["Anna", "Bram"], 25);
    const sideA2Texts = makeSideTexts(mainTextsA, ["Celine", "David"], 25);
    const sideB1Texts = makeSideTexts(mainTextsB, ["Eva", "Frank"], 25);
    const sideB2Texts = makeSideTexts(mainTextsB, ["Gina", "Hugo"], 25);
    const sideC1Texts = makeSideTexts(mainTextsC, ["Lena", "Niels"], 25);
    const sideC2Texts = makeSideTexts(mainTextsC, ["Sara", "Thomas"], 25);

    const storyMessages = [];

    const planForChat = (texts, chatId, characters, total, s, e, cl) => {
      const dates = distributeDates(total, s, e, cl, 0.6);
      for (let i = 0; i < total; i++) {
        const char = characters[i % characters.length];
        const timestamp = dates[i];
        const includeImage = Math.random() < 0.18;
        storyMessages.push({
          _id: new ObjectId(),
          ChatId: chatId,
          CharacterId: char._id,
          TextContent: texts[i % texts.length],
          FileContent: includeImage ? "image:pending" : null,
          PlannedAt: timestamp,
          CreatedAt: timestamp,
          Sent: timestamp <= new Date(),
          SentAt: timestamp <= new Date() ? timestamp : null,
        });
      }
    };

    planForChat(mainTextsA, chatsA.main, charsA, 50, start, end, climax);
    planForChat(sideA1Texts, chatsA.side1, [charsA[0], charsA[1]], 25, start, end, climax);
    planForChat(sideA2Texts, chatsA.side2, [charsA[2], charsA[3]], 25, start, end, climax);
    planForChat(mainTextsB, chatsB.main, charsB, 50, start, end, climax);
    planForChat(sideB1Texts, chatsB.side1, [charsB[0], charsB[1]], 25, start, end, climax);
    planForChat(sideB2Texts, chatsB.side2, [charsB[2], charsB[3]], 25, start, end, climax);
    planForChat(mainTextsC, chatsC.main, charsC, 50, startC, endC, climaxC);
    planForChat(sideC1Texts, chatsC.side1, [charsC[0], charsC[1]], 25, startC, endC, climaxC);
    planForChat(sideC2Texts, chatsC.side2, [charsC[2], charsC[3]], 25, startC, endC, climaxC);

    // upload some images for messages that requested them
    for (const msg of storyMessages) {
      if (msg.FileContent === "image:pending") {
        const source = Math.random() < 0.5 ? `https://picsum.photos/800/600` : `https://source.unsplash.com/random/800x600`;
        // best-effort upload; if fails, leave null
        // eslint-disable-next-line no-await-in-loop
        const hash = await uploadImageFromUrl(BUCKET_STORYMESSAGES, source);
        msg.FileContent = hash || null;
      }
    }

    await db.collection("StoryMessages").insertMany(storyMessages);

    // Posts + Comments: observer ("fly-on-the-wall") posts and comments only — admin will not create posts or comments
    const allChannels = [...channelsA, ...channelsB, ...channelsC];
    const posts = [];

    function observerPostTemplate(scenario, channelType, username) {
      if (scenario === "A") {
        if (channelType === "Onderzoek") {
          return `Als buitenstaander valt op dat de vondst van de rugzak veel losse eindjes samenbrengt. De combinatie van tips en CCTV-fragmenten creëert een gelaagd beeld; interessant om te zien wie welke bron inbrengt.`;
        }
        return `Het verhaal leest bijna als een reconstructie: tips komen binnen, verwijzingen naar beelden, en de groepsdynamiek zelf geeft veel context. Als observator vraag ik me af welke bronnen het meest betrouwbaar zijn.`;
      } else if (scenario === "B") {
        if (channelType === "Crisis") {
          return `Vanuit de marge is het indrukwekkend hoe burgers en hulpdiensten improviseren. Foto's van verlichte straten en oplaadpunten vertellen meer over veerkracht dan officiële statements soms doen.`;
        }
        return `De blackout-berichten tonen de typische patronen van een onverwachte ramp: eerst chaos, daarna snelle lokale coördinatie via informele netwerken. Het is leerzaam om dat proces van dichtbij te volgen.`;
      } else {
        if (channelType === "Geheimen") {
          return `De manier waarop geheimen jarenlang bewaard kunnen blijven binnen een vriendengroep is fascinerend en beklemmend tegelijk. Wie draagt de meeste last — degene die weet, of degene die niet weet?`;
        }
        return `Een reünie als setting voor het onthullen van een oud geheim werkt precies omdat iedereen tegelijk aanwezig is. De sociale druk en de gedeelde herinneringen maken ontkenning bijna onmogelijk.`;
      }
    }

    // Only non-admin users will create posts/comments (admin excluded)
    const nonAdminUsers = users.filter(u => u.Role !== "admin");

    for (const chId of allChannels) {
      const chStr = chId.toString();
      let scenario = "A";
      let channelType = "Algemeen";
      if (chStr === channelsA[1].toString()) { channelType = "Onderzoek"; scenario = "A"; }
      if (chStr === channelsB[0].toString()) { channelType = "Algemeen"; scenario = "B"; }
      if (chStr === channelsB[1].toString()) { channelType = "Crisis"; scenario = "B"; }
      if (chStr === channelsC[0].toString()) { channelType = "Algemeen"; scenario = "C"; }
      if (chStr === channelsC[1].toString()) { channelType = "Geheimen"; scenario = "C"; }

      for (const u of nonAdminUsers) {
        const title = `Observatie — ${u.Username}`;
        const content = observerPostTemplate(scenario, channelType, u.Username) + "\n\n(Observatie vanuit de marge; dit is geen oproep tot actie.)";
        // ChannelId wordt als string opgeslagen (chId.toString())
        // Het Post model heeft BsonRepresentation(BsonType.ObjectId), dus MongoDB converteert automatisch
        posts.push({ _id: new ObjectId(), Title: title, AuthorId: u._id, CreatedAt: new Date(), ChannelId: chId, Content: content, IsArchived: false });
      }
    }

    // Voeg extra posts toe met verschillende content
    const extraPosts = [
      {
        title: "Analyse van de tijdlijn",
        content: "Na het doorlopen van alle berichten valt op dat er bepaalde patronen zichtbaar zijn. De timing van bepaalde gebeurtenissen lijkt niet helemaal te kloppen met wat er wordt gemeld. Zou het kunnen dat er informatie wordt achtergehouden?",
        channelId: channelsA[1],
        authorId: nonAdminUsers[0]._id
      },
      {
        title: "Betrouwbaarheid van bronnen",
        content: "Ik vraag me af hoe betrouwbaar de verschillende bronnen zijn. Sommige getuigenissen lijken elkaar tegen te spreken. Is er al een verificatieproces gestart?",
        channelId: channelsA[1],
        authorId: nonAdminUsers[1]._id
      },
      {
        title: "Coördinatie tijdens de crisis",
        content: "Het is indrukwekkend om te zien hoe snel de gemeenschap zich organiseert tijdens een crisis. Zonder centrale sturing ontstaan er spontaan hulppunten en coördinatiecentra. Dit zegt veel over de veerkracht van de lokale bevolking.",
        channelId: channelsB[1],
        authorId: nonAdminUsers[0]._id
      },
      {
        title: "Lessen voor de toekomst",
        content: "Deze situatie toont aan hoe belangrijk het is om voorbereid te zijn. Wat kunnen we leren van hoe deze crisis wordt aangepakt? Zijn er structurele verbeteringen nodig in de infrastructuur?",
        channelId: channelsB[0],
        authorId: nonAdminUsers[1]._id
      },
      {
        title: "De rol van sociale media",
        content: "Interessant om te zien hoe sociale media zowel helpen als hinderen tijdens deze situatie. Aan de ene kant verspreidt informatie zich snel, aan de andere kant ontstaan er ook geruchten. Hoe kunnen we dit beter beheren?",
        channelId: channelsA[0],
        authorId: nonAdminUsers[0]._id
      }
    ];

    for (const extraPost of extraPosts) {
      posts.push({
        _id: new ObjectId(),
        Title: extraPost.title,
        AuthorId: extraPost.authorId,
        CreatedAt: new Date(),
        ChannelId: extraPost.channelId,
        Content: extraPost.content,
        IsArchived: false
      });
    }

    await db.collection("Posts").insertMany(posts);

    // Comments: only non-admin users comment. Tone: analytic/observational questions and short notes.
    const comments = [];
    const commentTemplates = [
      (u, p) => `Interessant punt in "${p.Title}" — wie controleert de bron van de foto?`,
      (u, p) => `Als buitenstaander vraag ik me af of de timestamps consistent zijn met het narratief in deze thread.`,
      (u, p) => `Opvallend hoe snel geruchten zich verspreiden; verificatie lijkt cruciaal hier.`,
      (u, p) => `Dit is een mooi voorbeeld van burgeractie — documentatie zoals foto's helpt vaak bij reconstructies.`,
      (u, p) => `Dank voor de observatie; het geeft context aan het grotere geheel.`
    ];

    for (const commenter of nonAdminUsers) {
      const candidatePosts = posts.filter(p => p.AuthorId.toString() !== commenter._id.toString());
      if (candidatePosts.length === 0) continue;
      const pick = candidatePosts[Math.floor(Math.random() * candidatePosts.length)];
      const template = commentTemplates[Math.floor(Math.random() * commentTemplates.length)];
      comments.push({ _id: new ObjectId(), Content: template(commenter, pick), AuthorId: commenter._id.toString(), PostId: pick._id.toString(), CreatedAt: new Date(), UpdatedAt: null });
    }

    await db.collection("Comments").insertMany(comments);

    // Seed a ticket for a test user so developers can enter the waiting room
    // Give the first non-admin user a ticket for Scenario C (past scenario)
    try {
      const testUser = nonAdminUsers[0];
      if (testUser) {
        const ticket = {
          _id: new ObjectId(),
          UserId: testUser._id,
          ScenarioId: scenarioCId,
          PurchasedAt: new Date(),
        };
        await db.collection("Tickets").insertOne(ticket);
        console.log(`✅ Inserted test ticket for user ${testUser.Username}`);
      }
    } catch (e) {
      console.warn("Could not insert test ticket:", e.message || e);
    }

    console.log("✅ Database seeded successfully!");
  } catch (e) {
    console.error(e);
  } finally {
    await client.close();
  }
}

seedDatabase();
