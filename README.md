I've been using CharaStudio for a few years now and somehow it never occurred to me that a character filtering mod might be useful.


Studio Chara Filter turns CharaStudio’s character list from a long list of names into something you can actually search. Filter thousands of cards by body sliders, outfits (towel, swimwear, nothing on…), zipmod content and missing mods, author, folder and name. It works through a background index, so no cards need to be loaded. It also makes CharaStudio itself faster: a 933-card folder that took 3.8 seconds to open now opens in 13 ms, and hovering over names no longer stutters. For collections with missing mods, it lists exactly which zipmods are missing, how many cards need each one, and where to get them.

There are a few features untested, VR has not been tested yet (sometimes windows to not like to render in VR). If you enable card removal, Windows deleting has not been tested. 


This plugin is primarily intended to enhance character searching, however it ended up potentially increasing the performance of character card loading, as it builds an index which is then referenced for each time CharaStudio opens. You may see significant reductions in load times for folders with higher card counts. Additionally, you can adjust the parameter from the default 1500, to higher or lower values which should correlate to equatable loading times (lower values lower loading times, theoretically). 

I have the plugin search through each body parameter, by default I have about 11 primarily based on bust + butt, but in F1 you can configure the full list of available body parts, from headsize to footsize. What the mod does is read the character card data, and then it builds an index to reference. This index should also generally help speed up Koikatsu character card loading, once the index has been built after the first time loading. 

So for example, in a list of 10 cards, you might have a height ranging from -10 to 100. The mod will read each character card, then starts with a default filter of range 1-100, raising or lowering. You can also add +range for additional custom filtering, i.e searching 10-20, 60-90 and it will show all cards with those ranges. 

The filters are additive, which means as you add more filters, they apply together. Potentially easy to be too restrictive, but so far I haven't had any issues. 

I hope this is beneficial to the community, it's my thanks for all the character cards and scenes over the years. 

