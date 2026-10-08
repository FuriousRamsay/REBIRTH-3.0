using System;using System.Collections.Generic; class ActualClaim { internal Scope Original; internal CraftCompleteData receipt; private bool claimed; private bool claiming;        internal bool MatchesReceipt()
        {
            if(!Original.IsCurrent()||Original.Station.CraftCompleteList==null)return false;
            int found=0;
            foreach(var row in Original.Station.CraftCompleteList)
            {
                if(!RebirthStationCompletionReceipt.HasReservedMarker(row))continue;
                if(!RebirthStationCompletionReceipt.TryReadIdentity(row,out var identity))return false;
                if(identity.Job!=Original.Admission.JobId)continue;
                if(++found!=1||row.CrafterEntityID!=receipt.CrafterEntityID||row.RecipeName!=receipt.RecipeName||
                    row.ItemScrapped!=receipt.ItemScrapped||row.CraftExpGain!=receipt.CraftExpGain||row.RecipeUsedCount!=receipt.RecipeUsedCount||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(row.CraftedItemStack,receipt.CraftedItemStack))return false;
            }
            return found==1;
        }        internal bool TryClaim(){if(claimed||!MatchesReceipt())return false;claimed=true;return true;}} class FixedClaim { internal Scope Original; internal CraftCompleteData receipt; private bool claimed; private bool claiming;        internal bool MatchesReceipt()
        {
            if(!Original.IsCurrent()||Original.Station.CraftCompleteList==null)return false;
            int found=0;
            foreach(var row in Original.Station.CraftCompleteList)
            {
                if(!RebirthStationCompletionReceipt.HasReservedMarker(row))continue;
                if(!RebirthStationCompletionReceipt.TryReadIdentity(row,out var identity))return false;
                if(identity.Job!=Original.Admission.JobId)continue;
                if(++found!=1||row.CrafterEntityID!=receipt.CrafterEntityID||row.RecipeName!=receipt.RecipeName||
                    row.ItemScrapped!=receipt.ItemScrapped||row.CraftExpGain!=receipt.CraftExpGain||row.RecipeUsedCount!=receipt.RecipeUsedCount||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(row.CraftedItemStack,receipt.CraftedItemStack))return false;
            }
            return found==1;
        }        internal bool TryClaim()
        {
            if(claimed||claiming)return false;
            claiming=true;
            try
            {
                if(!MatchesReceipt()||!Original.IsCurrent())return false;
                claimed=true;return true;
            }
            finally{claiming=false;}
        }}
