// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// Las 107 frases de Festi Plaza que el formulario <c>SAV_FestivalPlaza</c> de PKHeX enseña (las 106 comunes y la del rango 100, la ultima), copiadas tal cual de su codigo (version «UltraMoon»): PKHeX no las traduce
/// (el formulario entero queda en ingles, tambien con el idioma en castellano) y no hay una fuente oficial de esos textos en castellano, asi que se enseñan como PKHeX las trae (ADR 0064).
/// </summary>
internal static class FestiPlazaFrases
{
    private const string musical8note = "♪";
    private const string linedP = "₽";

    /// <summary>Las 106 frases comunes (indices 0 a 105) y, al final, la del rango 100 (indice 106).</summary>
    public static readonly string[] Textos =
    [ //source:UltraMoon
                    // (SM)Pokémon House
                    "There's nothing funny about Nuggets.","The Power of science is awesome.","1, 2, and... Ta-da!","How's the future Champ today?","Why, you!","There! All happy and healthy!","Your Pokémon seems to be very happy!","No thanks!","Would you like to use Cut?","Saving...",
                    // (SM)Kanto Tent
                    "Well, I better get going!","Bonjour!","Smell ya later!","Sorry! Bad call!","You better have Burn Heal!","Hoo hah!","Pokémon are for battling!","Slowbro took a snooze...","Shades of your journey await!","You're 10,000 light-years from facing Brock!","Hey! Wait! Don't go out!","Hiya! I'm a Pokémon...","What do you want?","WHAT! This can't be!","Mew!","Be gone... Intruders...",
                    // (SM)Joht Tent
                    "Dragonite, Hymer Beam.","Spread the fun around.","I bought an adorable doll with your money.","What are you staring at?","I just don't understand.","Everyone is into Pokémon.","I'm going to train 24 hours a day!","I'm the real deal!","With a jolt of electricity...","For 10 years I chased Suicune.","I am just so deeply moved!","We have finally made it!","...But selling Slowpoke Tails?","Shaoooh!","Gyaaas!","you've taken your first step!",
                    // (SM)Hoenn Tent
                    "I'm just the strongest there is right now.","And confoundedly elegant!","You guys need some imagination.","You made a much bigger splash!","You ended up giving me a thrill!","So what am I talking about...","I'm not buying any Dolls.","Take your time and rest up!","Have a blast, Pokémon Trainers!","I got ashes in my eyelashes!","You're sharp!","Number of collisions: 5 times!","Please! Help me out!","I can't see Mirage Island today...","Hyahhn!","Shwahhn!",
                    // (SM)Sinnoh Tent
                    "Your will is overwhelming me!","This is it! My trump card!","Trying to monopolize Pokémon just isn't...","See? Just as analyzed.","Gagyagyaah!","Gugyugubah!","It's a "+linedP+"10 million fine if you're late!","I'm going to get tougher...","You'll never be able to stem the flow of time!","Please come!","Your team! I sense your strong aura!","What do you think you're doing?!","The ring is my rolling sea. "+musical8note,"I was just thinking out loud.","My code name, it is Looker.","It's not possible that I lose!",
                    // (SM)Unova Tent
                    "Knock it off!","Bravo! Excellent!!","I'll defeat the Champion.","You're about to feel my rage!","Nimbasa's Pokémon can dance a nimble bossa!","Use Bug-type Pokémon!","Stop!","People call me Mr. Medal!","Trainer, do your best, too!","See who's stronger!","Plasbad, for short!","I won't allow anyone to stop me!","I was expecting exactly that kind of move!","Bazzazzazzash!","Preeeeaah!","Haaahraaan!",
                    // (SM)Kalos Tent
                    "We'll become friends. "+musical8note,"I'll see you all later!","These people have a few screws loose...","I want to know what a \"Trainer\" is.","When I lose, I go out in style!","Let's give it all we've got!","Fantastic! Just fantastic!","Outstanding!","Try as hard as possible!","Stop right there!","That really hit me right here...","But this is adieu to you all.","You're just too much, you know?","Fool! You silly, unseeing child!","Xsaaaaaah!","Yvaaaaaar!",
                    "I reached Festival Plaza Rank 100!",
                ];
}
