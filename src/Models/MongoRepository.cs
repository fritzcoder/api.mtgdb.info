using System;
using Mtg.Model;
using Nancy;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using Nancy.ModelBinding;
using Nancy.Json;
using System.Dynamic;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mtg
{
    public class MongoRepository : IRepository, IWriteRepository
    {
        private string Connection { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mtg.MongoRepository"/> class.
        /// </summary>
        /// <param name="connection">Connection.</param>
        public MongoRepository (string connection)
        {
            Connection = connection;
        }


        public async Task<string[]> GetCardRarity()
        {
            List<string> t = new List<string>();
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<Card> ("cards");

            var types = collection.Distinct("rarity");

            foreach(var type in types)
            {
                if(!type.IsBsonNull)
                {
                    t.Add(type.AsString);
                }
            }

            return t.ToArray();
        }
            
        public async Task<string[]> GetCardTypes()
        {
            List<string> t = new List<string>();
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<Card> ("cards");

            var types = collection.Distinct("type");

            foreach(var type in types)
            {
                if(!type.IsBsonNull)
                {
                    t.Add(type.AsString);  
                }
            }

            return t.ToArray();
        }

        public async Task<string[]> GetCardSubTypes()
        {
            List<string> t = new List<string>();
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<Card> ("cards");

            var types = collection.Distinct("subType");

            foreach(var type in types)
            {
                if(!type.IsBsonNull)
                {
                    t.Add(type.AsString);  
                }
            }

            return t.ToArray();
        }

        /// <summary>
        /// Gets the random card.
        /// </summary>
        /// <returns>The random card.</returns>
        public async Task<Card> GetRandomCard()
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<Card> ("cards");
            int max =           collection.AsQueryable<Card>()
                                .Select(c => c.Id)
                                .Max();

            Random rand =       new Random();

            var query =         Query<Card>.EQ (e => e.Id, rand.Next(max));
            Card card =         collection.FindOne (query);
            while(card == null)
            {
                query =         Query<Card>.EQ (e => e.Id, rand.Next(max));
                card =          collection.FindOne (query);
            }

            return card;
        }

        /// <summary>
        /// Gets the random card in set.
        /// </summary>
        /// <returns>The random card in set.</returns>
        /// <param name="setId">Set identifier.</param>
        public async Task<Card> GetRandomCardInSet(string setId)
        {
            CardSet cSet =      this.GetSet(setId).Result;
            Card card = null;
            if(cSet != null && cSet.CardIds != null)
            {
                Random rand =       new Random();
                card =              this.GetCard(cSet.CardIds[rand.Next(0, 
                    cSet.CardIds.Length-1)]).Result;
            }

            return card;
        }
        public async Task<Card> GetCardBySetNumber (string setId, int setNumber)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");

            var collection =    database.GetCollection<Card> ("cards");

            var query =         Query.And (Query<Card>.EQ(x => x.CardSetId, setId.ToUpper()),
                Query<Card>.EQ(x => x.SetNumber, setNumber));

            Card card =       collection.FindOne (query);

            return card;
        }

        /// <summary>
        /// Gets the sets.
        /// </summary>
        /// <returns>The sets.</returns>
        /// <param name="setIds">Set identifiers.</param>
        public async Task<CardSet[]> GetSets (string [] setIds)
        {
            List<CardSet> cardset = new List<CardSet> ();
            var client =            new MongoClient (Connection);
            var server =            client.GetServer ();
            var database =          server.GetDatabase ("mtg");
            setIds =                setIds.Select (x => x.ToUpper ()).ToArray();
            var collection =        database.GetCollection<CardSet> ("card_sets");

            var query =             Query.In ("_id", new BsonArray(setIds));
            var sets =              collection.Find (query);

            foreach (CardSet set in sets) 
            {
                cardset.Add (set);
            }

            return cardset.ToArray ();
        }
        /// <summary>
        /// Gets the cards.
        /// </summary>
        /// <returns>The cards.</returns>
        /// <param name="multiverseIds">Multiverse identifiers.</param>
        public async Task<Card[]> GetCards (int [] multiverseIds)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");

            var collection =    database.GetCollection<Card> ("cards");

            var query =         Query.In ("_id", new BsonArray(multiverseIds));
            var dbcards =       collection.Find (query);
            List<Card> cards =  new List<Card> ();

            foreach(Card c in dbcards)
            {
                cards.Add (c);
            }

            return cards.ToArray ();
        }

        public async Task<long> SearchTotal (string text, bool isComplex = false)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");

            var collection =    database.GetCollection<Card> ("cards");

            List<Card> cards =  new List<Card> ();
            MongoCursor<Card> cursor = null;

            IMongoQuery query = null; 

            if(isComplex)
            {
                CardSearch search = new CardSearch(text);
                query = Query.And (search.MongoQuery());
            }
            else
            {
                query = Query<Card>.Matches (e => e.SearchName, 
                    text.ToLower().Replace(" ", ""));
            }

            return collection.Count(query);
        }
            
        /// <summary>
        /// Search the specified text.
        /// </summary>
        /// <param name="text">Text.</param>
        public async Task<Card[]> Search (string text, 
            int start = 0, 
            int limit = 0, 
            bool isComplex = false)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");

            var collection =    database.GetCollection<Card> ("cards");

            List<Card> cards =  new List<Card> ();
            MongoCursor<Card> cursor = null;

            IMongoQuery query = null; 

            if(isComplex)
            {
                CardSearch search = new CardSearch(text);
                query = Query.And (search.MongoQuery());
            }
            else
            {
                query = Query<Card>.Matches (e => e.SearchName, 
                    text.ToLower().Replace(" ", ""));

            }

            cursor = collection.Find (query)
                .SetSortOrder ("_id")
                .SetSkip(start)
                .SetLimit(limit);

            foreach (Card card in cursor) 
            {
                cards.Add (card);
            }
           
            return cards.ToArray ();

        }
        /// <summary>
        /// Gets the cards.
        /// </summary>
        /// <returns>The cards.</returns>
        /// <param name="query">Query.</param>
        public async Task<Card[]> GetCards (dynamic query)
        {
            var client =                new MongoClient (Connection);
            var server =                client.GetServer ();
            var database =              server.GetDatabase ("mtg");
            var collection =            database.GetCollection<Card> ("cards");

            MongoCursor<Card> cursor =  null;                
            List<IMongoQuery> queries = new List<IMongoQuery> ();


            if (query.cardsetid != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.CardSetId, 
                                           (string)query.cardsetid));
            }

            if (query.artist != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.Artist, 
                                           (string)query.artist));
            }

            if (query.rarity != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.Rarity, 
                                           (string)query.rarity));
            }
  
            if (query.loyalty != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.Loyalty, 
                                           (int)query.loyalty));
            }

            if (query.toughness != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.Toughness, 
                                           (int)query.toughness));
            }

            if (query.power != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.Power, 
                                           (int)query.power));
            }

            if (query.subtype != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.SubType, 
                                           (string)query.subtype));
            }


            if (query.cardsetname != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.CardSetName, 
                                           (string)query.cardsetname));
            }

            if (query.convertedmanacost != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.ConvertedManaCost, 
                                           (int)query.convertedmanacost));
            }

            if (query.setnumber != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.SetNumber, 
                                           (int)query.setnumber));
            }

            if (query.manacost != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.ManaCost, 
                                           (string)query.manacost));
            }

            if (query.colors != null) 
            {
                foreach (string color in ((string)query.colors).ToString().Split(',')) 
                {
                    queries.Add (Query<Card>.EQ (e => e.Colors, color));
                }
            }

            if (query.name != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.SearchName, 
                    ((string)query.name).ToLower().Replace(" ", "")));
            }

            if (query.type != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.Type, (string)query.type));
            }

            if (query.id != null) 
            {
                queries.Add (Query<Card>.EQ (e => e.Id, (int)query.id));
            }

            if (queries.Count > 0) 
            {
                cursor = collection.Find (Query.And (queries)).SetSortOrder ("_id");
            } 
            else 
            {
                cursor = collection.FindAllAs<Card> ().SetSortOrder ("_id");
            }

            if (query.limit != null) 
            {
                cursor.SetLimit ((int)query.limit);
            }

            List<Card> cards = new List<Card> ();

            foreach (Card card in cursor) 
            {
                cards.Add (card);
            }

            return cards.ToArray ();
        }
        /// <summary>
        /// Gets the cards by set.
        /// </summary>
        /// <returns>The cards by set.</returns>
        /// <param name="setId">Set identifier.</param>
        /// <param name="start">Start.</param>
        /// <param name="end">End.</param>
        public async Task<Card[]> GetCardsBySet (string setId, 
            int start = 0, 
            int end = 0)
        {
            List<Card> cards =          new List<Card> ();
            var client =                new MongoClient (Connection);
            var server =                client.GetServer ();
            var database =              server.GetDatabase ("mtg");

            var collection =            database.GetCollection<Card> ("cards");

            List<IMongoQuery> queries = new List<IMongoQuery> ();

            if (start > 0) 
            {
                queries.Add (Query<Card>.GTE (c => c.SetNumber, start));
            }

            if(end > 0)
            {
                queries.Add (Query<Card>.LTE (c => c.SetNumber, end));
            }
    
            queries.Add (Query<Card>.EQ (e => e.CardSetId, (setId).ToUpper ()));
            //var query = Query<Card>.EQ (e => e.CardSetId, (setId).ToUpper ());
            MongoCursor<Card> cursor = 
                collection.Find (Query.And(queries))
                    .SetSortOrder ("setNumber");

            foreach (Card card in cursor) 
            {
                cards.Add (card);
            }

            return cards
                    .Where(x => x.Token == false)
                    //.Where(x => x.Promo == false)
                    .ToArray();
        }

        /// <summary>
        /// Gets the card.
        /// </summary>
        /// <returns>The card.</returns>
        /// <param name="id">Identifier.</param>
        public async Task<Card> GetCard (int id)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");

            var collection =    database.GetCollection<Card> ("cards");
            var query =         Query<Card>.EQ (e => e.Id, id);
            Card card =         collection.FindOne (query);

            return card;
        }
        /// <summary>
        /// Gets the cards.
        /// </summary>
        /// <returns>The cards.</returns>
        /// <param name="name">Name.</param>
        public async Task<Card[]> GetCards (string name)
        {
            var client = new MongoClient (Connection);
            var server = client.GetServer ();
            var database = server.GetDatabase ("mtg");

            Regex rgx = new Regex("[^a-zA-Z0-9]");
            name = rgx.Replace(name, "");

            var collection = database.GetCollection<Card> ("cards");
            var query = Query<Card>.EQ (e => e.SearchName, name.ToLower().Replace(" ", ""));
            MongoCursor<Card> cursor = collection.Find (query);

            List<Card> cards = new List<Card> ();

            foreach (Card card in cursor) 
            {
                cards.Add (card);
            }

            return cards.ToArray ();
        }

        /// <summary>
        /// Gets the sets.
        /// </summary>
        /// <returns>The sets.</returns>
        public async Task<CardSet[]> GetSets ()
        {
            List<CardSet> cardset = new List<CardSet> ();
            var client =            new MongoClient (Connection);
            var server =            client.GetServer ();
            var database =          server.GetDatabase ("mtg");

            var collection =        database.GetCollection<CardSet> ("card_sets");

            MongoCursor<CardSet> cursor = collection.FindAllAs<CardSet> ()
                .SetSortOrder ("name");

            foreach (CardSet set in cursor) 
            {
                cardset.Add (set);
            }

            return cardset.OrderBy(x => x.ReleasedAt).ToArray ();
        }
        /// <summary>
        /// Gets the set.
        /// </summary>
        /// <returns>The set.</returns>
        /// <param name="id">Identifier.</param>
        public async Task<CardSet> GetSet (string id)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<CardSet> ("card_sets");
            var query =         Query<CardSet>.EQ (e => e.Id, id.ToUpper ());
            CardSet cardset =   collection.FindOne (query);

            return cardset;
        }


        /// <summary>
        /// Updates the card fields do not use to update card Rulings.
        /// </summary>
        /// <returns>The card.</returns>
        /// <param name="mvid">Mvid.</param>
        /// <param name="field">the mongodb field name</param>
        /// <param name="value">Value.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public async Task<Card> UpdateCardField<T>(int mvid, string field, T value)
        {
            var client =            new MongoClient (Connection);
            var server =            client.GetServer ();
            var database =          server.GetDatabase ("mtg");
            var collection =        database.GetCollection<Card> ("cards");
            var query =             Query.EQ ("_id", mvid);
            var sortBy =            SortBy.Descending("_id");
               
            UpdateBuilder update =  new UpdateBuilder ();
          
            update = Update
                .Set(field, BsonValue.Create (value));
                
            var result = collection.FindAndModify(
                query,
                sortBy,
                update
            );
           
            return GetCard (mvid).Result;
        }

        /// <summary>
        /// Updates the card rulings. This method will replace the card rulings with the new card rulings.
        /// Make sure all rulings are included. 
        /// </summary>
        /// <returns>The card rulings.</returns>
        /// <param name="mvid">Mvid.</param>
        /// <param name="rulings">Rulings.</param>
        public async Task<Card> UpdateCardRulings (int mvid, Ruling[] rulings)
        {
            rulings = rulings.OrderBy (x => x.ReleasedAt).ToArray();
            BsonArray newRulings = new BsonArray ();

            int id = 1; 
            foreach(Ruling rule in rulings)
            {
           
                newRulings.Add (new BsonDocument
                {
                    {"_id", id},
                    {"releasedAt", rule.ReleasedAt},
                    {"rule", rule.Rule}
                });

                ++id;
            }

            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<Card> ("cards");
            var query =         Query.EQ ("_id", mvid);
            var sortBy =        SortBy.Descending("_id");

            var update = Update
                .Set("rulings", newRulings);

            var result = collection.FindAndModify(
                query,
                sortBy,
                update
            );

            return GetCard (mvid).Result;
        }

        public async Task<Card> UpdateCardFormats (int mvid, Format[] formats)
        {
            formats =               formats.OrderBy (x => x.Name).ToArray();
            BsonArray newFormats = new BsonArray ();

            int id = 1; 
            foreach(Format format in formats)
            {
                newFormats.Add (new BsonDocument
                {
                    {"_id", id},
                    {"name", format.Name},
                    {"legality", format.Legality}
                });

                ++id;
            }

            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<Card> ("cards");
            var query =         Query.EQ ("_id", mvid);
            var sortBy =        SortBy.Descending("_id");

            var update = Update
                .Set("formats", newFormats);

            var result = collection.FindAndModify(
                query,
                sortBy,
                update
            );

            return GetCard (mvid).Result;
        }

        public async Task<Card> AddCard(Card newCard)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<Card> ("cards");

            CardSet set = await this.GetSet(newCard.CardSetId);
            newCard.CardSetName = set.Name;

            collection.Save(newCard);

            Card card = await this.GetCard(newCard.Id);

            return card;
        }

        public async Task<CardSet> AddCardSet(CardSet newSet)
        {
            var client =        new MongoClient (Connection);
            var server =        client.GetServer ();
            var database =      server.GetDatabase ("mtg");
            var collection =    database.GetCollection<CardSet> ("card_sets");

            collection.Save(newSet);

            CardSet set = await this.GetSet(newSet.Id);

            return set;
        }

        public async Task<CardSet> UpdateCardSet<T>(string id, string field, T value)
        {
            var client =            new MongoClient (Connection);
            var server =            client.GetServer ();
            var database =          server.GetDatabase ("mtg");
            var collection =        database.GetCollection<CardSet> ("card_sets");
            var query =             Query.EQ ("_id", id);
            var sortBy =            SortBy.Descending("_id");

            UpdateBuilder update =  new UpdateBuilder ();

            update = Update
                .Set(field, BsonValue.Create (value));

            var result = collection.FindAndModify(
                query,
                sortBy,
                update
            );

            return GetSet (id).Result;
        }
    }
}

