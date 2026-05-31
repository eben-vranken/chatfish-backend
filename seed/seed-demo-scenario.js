#!/usr/bin/env node
// Demo seed: voegt één nieuw scenario toe dat begint over 3 minuten.
// Verwijdert GEEN bestaande data. Veilig om meerdere keren te runnen.
// Berichten worden minuut per minuut vrijgegeven vanaf het startmoment.

const { MongoClient, ObjectId } = require("mongodb");
const crypto = require("crypto");
const https = require("https");
const http = require("http");
const { Client } = require("minio");

const DEFAULT_CONNECTION_STRING =
  process.argv[2] || process.env.MONGODB_CONNECTION_STRING || "mongodb://localhost:27017";
const DEFAULT_DATABASE_NAME =
  process.argv[3] || process.env.MONGODB_DATABASE_NAME || "ChatfishDb";
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
  } catch {
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

// Groepschat berichten — 50 stuks, minuut per minuut vrijgegeven
const mainTexts = [
  "De notaris is gearriveerd. Iedereen wordt verzocht zich in de salon te verzamelen voor de lezing van het testament.",
  "Viktor is er al. Hij staat bij het raam en zegt bijna niets. Dat is ongewoon voor hem.",
  "Elise heeft de oude familiefoto's op tafel gelegd. Ze zegt dat ze 'context wil scheppen' maar ik vraag me af wat ze zoekt.",
  "De notaris opent de envelop. Er valt een stilte over de kamer die bijna tastbaar is.",
  "Het testament dateert van drie maanden geleden — twee weken voor grootvaders overlijden. Vreemd.",
  "Pieter vraagt of er een eerdere versie bestaat. De notaris aarzelt merkbaar voor hij antwoordt.",
  "Noor heeft de inventarislijst van het huis gevonden. Meerdere waardevolle objecten ontbreken.",
  "Viktor reageert kwaad wanneer de notaris de verdeling voorleest. Hij had duidelijk andere verwachtingen.",
  "Elise fluistert iets in Pieters oor. Hij wordt bleek.",
  "De notaris neemt een pauze. Hij belt discreet in de gang — wie belt hij?",
  "Noor heeft in de boekenkast een verborgen lade gevonden. Erin zitten brieven die niemand kende.",
  "De brieven zijn gericht aan een vrouw die Marguerite heet. Niemand in de familie kent die naam.",
  "Viktor eist de brieven op. Elise weigert ze los te laten zonder ze eerst te lezen.",
  "Er staat in één brief dat grootvader een rekening heeft geopend op naam van Marguerite in 2019.",
  "Pieter zoekt op zijn telefoon. Marguerite Deschamps — dat is een naam die voorkomt in een oud krantenartikel uit 1987.",
  "Het artikel gaat over een erfenisgeschil. Een vrouw claimt een deel van het Deschamps-vermogen.",
  "Viktor verlaat abrupt de kamer. Wanneer hij terugkomt, ziet hij er gespannen uit.",
  "Elise heeft de nummering van de pagina's in het testament gecheckt. Pagina 4 ontbreekt.",
  "De notaris geeft toe dat hij het testament zoals hij het ontving heeft geopend. Hij heeft niets gewijzigd.",
  "Noor vindt in de brieven een adres in Gent. Mogelijk woont Marguerite daar nog.",
  "Viktor ontkent dat hij ooit van Marguerite heeft gehoord. Maar hij herkende haar naam — dat was zichtbaar.",
  "Pieter belt zijn moeder voor meer context. Ze hangt onmiddellijk op.",
  "In de kelder ontdekt Elise een archiefkast met de originele familiedocumenten.",
  "Tussen de documenten zit een notariële akte uit 1988: Marguerite Deschamps heeft destijds afgezien van haar erfrecht.",
  "Maar de handtekening op de akte lijkt niet van haar — de letters zijn onregelmatig, alsof iemand haar naam namens haar heeft gezet.",
  "Noor heeft Marguerite gevonden op sociale media. Ze leeft nog. Ze woont in Gent.",
  "Viktor probeert het gesprek te beëindigen en vraagt iedereen naar huis te gaan.",
  "Elise weigert. Ze zegt dat ze niet weggaat totdat de volledige waarheid op tafel ligt.",
  "De notaris laat weten dat hij verplicht is aangifte te doen als hij vermoedt dat documenten vervalst zijn.",
  "Pieter stuurt Marguerite een bericht via sociale media. Hij wacht op antwoord.",
  "Viktor belt opnieuw iemand. Noor hoort hem fluisteren: 'het loopt uit de hand'.",
  "Marguerite reageert. Ze schrijft dat ze al jaren wacht op dit moment.",
  "Ze heeft documenten bewaard die aantonen dat haar handtekening in 1988 vervalst werd.",
  "Elise vraagt de notaris om het testament voorlopig niet te registreren. Hij stemt in.",
  "Viktor gooit zijn glas neer en beschuldigt Elise ervan alles te saboteren.",
  "Pieter stelt voor dat iedereen zich kalmeert en dat ze Marguerite uitnodigen om haar kant te vertellen.",
  "Noor vindt in de archiefkast ook een brief van grootvader zelf — geschreven maar nooit verstuurd.",
  "In de brief staat dat hij spijt had van wat er in 1988 is gedaan, en dat hij het wilde rechtzetten.",
  "Het wordt duidelijk dat het recente testament mogelijk een poging was om alsnog rechtvaardigheid te doen.",
  "Viktor begrijpt nu dat hij de grote verliezer dreigt te worden — en dat hij het al die tijd heeft geweten.",
  "Elise leest de brief hardop voor. De kamer is stil. Niemand onderbreekt haar.",
  "Pieter vraagt Viktor rechtstreeks: 'Wist jij dit?' Viktor antwoordt niet.",
  "Marguerite stuurt foto's door van de documenten die ze heeft bewaard. Ze zijn overtuigend.",
  "De notaris bekijkt de documenten en bevestigt dat ze authentiek lijken.",
  "Viktor verlaat opnieuw de kamer. Dit keer keert hij niet snel terug.",
  "Elise en Pieter besluiten samen een advocaat te contacteren voor een formele procedure.",
  "Noor haalt Viktor terug naar de salon. Hij ziet er gebroken uit.",
  "Viktor geeft toe dat zijn vader hem destijds heeft gevraagd de handtekening te vervalsen. Hij was zeventien.",
  "De kamer absorbeert die woorden langzaam. Niemand weet hoe te reageren.",
  "De notaris vraagt iedereen morgen terug te komen. De lezing wordt uitgesteld tot de waarheid volledig boven tafel ligt.",
];

// Privégesprek Viktor & Elise (25 berichten)
const sideTexts1 = [
  "Elise, ik moet je iets vertellen voordat dit escaleert.",
  "Wat bedoel je? Je gezicht staat al de hele ochtend op onweer.",
  "Ik wist dat het testament niet klopte. Ik heb het niet aangekaart omdat ik dacht dat het te laat was.",
  "Viktor. Dat is niet iets wat je zomaar voor jezelf houdt.",
  "Mijn vader heeft me gevraagd te zwijgen. Ik was bang. Dat blijft zo.",
  "En toch ben je hier. Je had weg kunnen blijven.",
  "Ik kon het niet laten liggen. Elke keer als ik eraan dacht...",
  "Weet je wie Marguerite is? Echt?",
  "Ze is de dochter van een vrouw die grootvader voor zijn huwelijk heeft gekend. Een buitenechtelijk kind, denk ik.",
  "Dan is ze familie. Bloedverwant.",
  "Ja. En mijn grootvader heeft haar moeder jarenlang betaald om te zwijgen.",
  "Dat verklaart de rekening uit de brieven.",
  "Elise, als dit uitkomt, verlies ik alles. Mijn reputatie, de zaak, het vertrouwen van de familie.",
  "Je hebt al zeventien jaar iets gedragen wat nooit van jou was. Het is tijd om het neer te leggen.",
  "Wat als Marguerite de hele erfenis opeist?",
  "Dan heeft ze er misschien recht op. Dat is niet aan ons om te beslissen.",
  "Ik ben niet klaar voor die consequentie.",
  "Of je er klaar voor bent of niet — het komt. De vraag is hoe je het tegemoet treedt.",
  "Je klinkt als een rechter.",
  "Ik klink als iemand die wil dat haar familie uit de gevangenis blijft.",
  "Wat moet ik doen?",
  "Begin met de notaris te vertellen wat je weet. Alles.",
  "En Pieter? Hij weet niets van dit alles.",
  "Pieter houdt van jou. Dat verandert niet door de waarheid.",
  "Ik hoop dat je gelijk hebt.",
];

// Privégesprek Pieter & Noor (25 berichten)
const sideTexts2 = [
  "Noor, heb jij het gevoel dat Viktor iets verbergt?",
  "Dat gevoel heb ik al sinds we aankwamen. Hij staat de hele tijd met zijn rug naar ons toe.",
  "Hij heeft twee keer gebeld vanmorgen. Buiten, zodat niemand het hoorde.",
  "Ik heb zijn gezicht gezien toen de naam Marguerite viel. Hij kende haar.",
  "Wie is zij? Jij kent de familiegeschiedenis beter dan ik.",
  "Dat dacht ik ook, maar deze naam is nieuw voor mij.",
  "Die brieven in de boekenkast — ik denk dat grootvader ze bewust heeft laten liggen.",
  "Zodat iemand ze zou vinden?",
  "Hij was oud en ziek maar niet seniel. Als hij wilde dat ze verdwenen waren, had hij ze verbrand.",
  "Dus hij wou dat dit uitkwam.",
  "Misschien kon hij het zelf niet meer rechtzetten en rekende hij op ons.",
  "Op jou en mij, bedoel je. Niet op Viktor.",
  "Elise weet meer dan ze laat blijken. Maar ik geloof dat ze aan de goede kant staat.",
  "Ze houdt de notaris ook scherp. Dat is niet niets.",
  "Pieter, wat als er iemand bestaat die meer recht heeft op dit alles dan wij?",
  "Dan moet dat erkend worden. Dat is het enige eerlijke antwoord.",
  "Zelfs als dat betekent dat we minder krijgen?",
  "Ik ben niet hier voor het geld. Ik ben hier voor grootvader.",
  "Dat weet ik. Daarom ben ik blij dat jij degene bent die ik heb meegebracht.",
  "Ik ga de archiefkast volledig doorzoeken. Er moet meer zijn.",
  "Ik doe mee. Vier ogen zien meer.",
  "Als Viktor probeert iets achter te houden, wil ik het weten.",
  "We houden elkaar op de hoogte. Geen geheimen tussen ons.",
  "Nooit geweest, wordt nooit.",
  "Wat er ook uitkomt vanavond — we staan samen.",
];

async function seedDemoScenario() {
  const client = new MongoClient(DEFAULT_CONNECTION_STRING);
  try {
    await client.connect();
    const db = client.db(DEFAULT_DATABASE_NAME);

    // Zorg dat MinIO buckets bestaan
    for (const bucket of [BUCKET_CHARACTERS, BUCKET_CHATS, BUCKET_STORYMESSAGES]) {
      if (!(await minioClient.bucketExists(bucket))) {
        await minioClient.makeBucket(bucket);
        console.log(`✅ Bucket aangemaakt: ${bucket}`);
      }
    }

    // Gebruik bestaande admin — maak er één aan als die er niet is
    let adminUser = await db.collection("Users").findOne({ Role: "admin" });
    let nonAdminUsers = await db.collection("Users").find({ Role: "user" }).toArray();

    if (!adminUser) {
      const bcrypt = require("bcrypt");
      adminUser = { _id: new ObjectId(), Username: "admin", Email: "admin@chatfish.be", Password: await bcrypt.hash("admin123", 10), Role: "admin" };
      await db.collection("Users").insertOne(adminUser);
      console.log("✅ Admin user aangemaakt (bestond nog niet)");
    }
    if (nonAdminUsers.length === 0) {
      const bcrypt = require("bcrypt");
      const u = { _id: new ObjectId(), Username: "jan.jansen", Email: "jan.jansen@example.com", Password: await bcrypt.hash("wachtwoord123", 10), Role: "user" };
      await db.collection("Users").insertOne(u);
      nonAdminUsers = [u];
      console.log("✅ Test user aangemaakt (bestond nog niet)");
    }

    // Scenario start over exact 3 minuten
    const startMoment = new Date(Date.now() + 10 * 1000);

    const scenarioId = new ObjectId();
    await db.collection("Scenarios").insertOne({
      _id: scenarioId,
      Name: "Erfenis in het Donker",
      Description: "Een rijke erfenis, vier neven en nichten, en één nacht om de waarheid te ontdekken",
      CreatedBy: adminUser._id,
      StartMoment: startMoment,
      DurationMinutes: 90,
      Price: 14.00,
      SaleStatus: "open",
    });
    console.log(`✅ Scenario aangemaakt — start om ${startMoment.toLocaleTimeString("nl-BE")}`);

    // Personages
    const characterNames = ["Viktor Deschamps", "Elise Mortier", "Pieter Vandermeersch", "Noor Aerts"];
    const chars = [];
    for (const name of characterNames) {
      console.log(`   Profielfoto ophalen voor ${name}...`);
      const hash = await uploadRandomImage(BUCKET_CHARACTERS);
      chars.push({ _id: new ObjectId(), Name: name, ScenarioId: scenarioId, ProfilePicture: hash });
    }
    await db.collection("Characters").insertMany(chars);
    console.log("✅ Personages aangemaakt");

    // Kanalen
    const channelIds = [new ObjectId(), new ObjectId()];
    await db.collection("Channels").insertMany([
      { _id: channelIds[0], ChannelName: "Algemeen", ChannelDescription: "Hoofd channel voor het scenario", ScenarioId: scenarioId },
      { _id: channelIds[1], ChannelName: "Erfenis & Documenten", ChannelDescription: "Analyse van bewijsmateriaal en documenten", ScenarioId: scenarioId },
    ]);
    console.log("✅ Kanalen aangemaakt");

    // Chats
    console.log("   Chatfoto's ophalen...");
    const chatPic1 = await uploadRandomImage(BUCKET_CHATS);
    const chatPic2 = await uploadRandomImage(BUCKET_CHATS);
    const chatIds = { main: new ObjectId(), side1: new ObjectId(), side2: new ObjectId() };
    await db.collection("Chats").insertMany([
      { _id: chatIds.main, Name: "Groepschat Erfenis", ScenarioId: scenarioId, ProfilePicture: chatPic1 },
      { _id: chatIds.side1, Name: "Privé: Viktor & Elise", ScenarioId: scenarioId, ProfilePicture: chatPic2 },
      { _id: chatIds.side2, Name: "Privé: Pieter & Noor", ScenarioId: scenarioId, ProfilePicture: chatPic2 },
    ]);
    console.log("✅ Chats aangemaakt");

    // Berichten — elk bericht 1 minuut na het vorige, te beginnen bij startMoment
    const storyMessages = [];

    const planMinuteByMinute = (texts, chatId, characters) => {
      for (let i = 0; i < texts.length; i++) {
        const timestamp = new Date(startMoment.getTime() + i * 60 * 1000);
        const char = characters[i % characters.length];
        storyMessages.push({
          _id: new ObjectId(),
          ChatId: chatId,
          CharacterId: char._id,
          TextContent: texts[i],
          FileContent: null,
          PlannedAt: timestamp,
          CreatedAt: timestamp,
          Sent: timestamp <= new Date(),
          SentAt: timestamp <= new Date() ? timestamp : null,
        });
      }
    };

    planMinuteByMinute(mainTexts, chatIds.main, chars);
    planMinuteByMinute(sideTexts1, chatIds.side1, [chars[0], chars[1]]);
    planMinuteByMinute(sideTexts2, chatIds.side2, [chars[2], chars[3]]);

    await db.collection("StoryMessages").insertMany(storyMessages);
    console.log(`✅ ${storyMessages.length} berichten ingepland (1 per minuut)`);

    // Observer posts in kanalen
    const postTemplates = [
      {
        channelIdx: 0,
        title: "Eerste indrukken bij de lezing",
        content: "De dynamiek in de salon is opvallend: terwijl de notaris spreekt, lijkt elke reactie zorgvuldig gecalculeerd. Familiaire spanningen worden zelden zo zichtbaar als wanneer geld en erfrecht ter sprake komen. Als buitenstaander valt op dat de lichaamstaal meer zegt dan de woorden.",
      },
      {
        channelIdx: 1,
        title: "Analyse van de documentenreeks",
        content: "De chronologie van de documenten roept vragen op. Een testament dat twee weken voor overlijden wordt opgesteld, een akte met een betwiste handtekening uit 1988, en brieven die bewust zijn achtergelaten — dit lijkt geen toeval. De vraag is niet alleen wat er is gebeurd, maar ook of iemand dit heeft voorzien.",
      },
      {
        channelIdx: 0,
        title: "De rol van de notaris",
        content: "Interessant om te observeren hoe de notaris navigeert tussen zijn wettelijke plichten en de druk van de familie. Hij aarzelt op momenten die cruciaal zijn — niet uit onzekerheid, maar eerder omdat hij meer weet dan hij zegt. Dat is een patroon dat de moeite waard is om te volgen.",
      },
      {
        channelIdx: 1,
        title: "Vergelijkbare erfenisgeschillen",
        content: "Gevallen waarbij handtekeningen worden betwist in erfeniskwesties zijn zeldzaam maar niet uniek. Wat dit dossier bijzonder maakt, is de combinatie van een buitenechtelijk kind, een vervalste akte en een notaris die lijkt te aarzelen. De komende uren zullen bepalen of dit juridisch eskalatie vereist.",
      },
    ];

    const posts = [];
    for (const tmpl of postTemplates) {
      for (const u of nonAdminUsers) {
        posts.push({
          _id: new ObjectId(),
          Title: tmpl.title,
          AuthorId: u._id,
          CreatedAt: new Date(),
          ChannelId: channelIds[tmpl.channelIdx],
          Content: tmpl.content + `\n\n(Observatie van ${u.Username} — geen oproep tot actie.)`,
          IsArchived: false,
        });
      }
    }
    await db.collection("Posts").insertMany(posts);
    console.log(`✅ ${posts.length} posts aangemaakt`);

    // Comments
    const commentTemplates = [
      (p) => `De tijdlijn in "${p.Title}" sluit goed aan bij wat er in de chatberichten zichtbaar wordt — de volgorde van onthullingen lijkt bewust gestructureerd.`,
      (p) => `Goed opgemerkt. De rol van de notaris is inderdaad ambigu — iemand met die achtergrond weet precies wanneer hij moet zwijgen.`,
      (p) => `Als je de brieven en de akte naast elkaar legt, zie je een patroon. De data kloppen niet met de officiële lezing. Meer documentatie zou helpen.`,
      (p) => `Wat opvalt bij "${p.Title}": de manier waarop vertrouwelijke informatie naar buiten sijpelt via informele kanalen. Dat is typisch voor dit soort conflicten.`,
    ];
    const comments = [];
    for (const commenter of nonAdminUsers) {
      const candidates = posts.filter((p) => p.AuthorId.toString() !== commenter._id.toString());
      if (candidates.length === 0) continue;
      const pick = candidates[Math.floor(Math.random() * candidates.length)];
      const tmpl = commentTemplates[Math.floor(Math.random() * commentTemplates.length)];
      comments.push({
        _id: new ObjectId(),
        Content: tmpl(pick),
        AuthorId: commenter._id.toString(),
        PostId: pick._id.toString(),
        CreatedAt: new Date(),
        UpdatedAt: null,
      });
    }
    if (comments.length > 0) {
      await db.collection("Comments").insertMany(comments);
      console.log(`✅ ${comments.length} comments aangemaakt`);
    }

    // Ticket voor eerste niet-admin gebruiker
    const ticketUser = nonAdminUsers[0];
    await db.collection("Tickets").insertOne({
      _id: new ObjectId(),
      UserId: ticketUser._id,
      ScenarioId: scenarioId,
      PurchasedAt: new Date(),
    });
    console.log(`✅ Ticket aangemaakt voor ${ticketUser.Username}`);

    console.log("");
    console.log("🎬 Demo scenario klaar!");
    console.log(`   Naam:   Erfenis in het Donker`);
    console.log(`   Start:  ${startMoment.toLocaleString("nl-BE")} (over 10 seconden)`);
    console.log(`   Chats:  ${storyMessages.length} berichten, elk 1 minuut apart`);
  } catch (e) {
    console.error("❌ Fout tijdens aanmaken demo scenario:", e);
    process.exit(1);
  } finally {
    await client.close();
  }
}

seedDemoScenario();
