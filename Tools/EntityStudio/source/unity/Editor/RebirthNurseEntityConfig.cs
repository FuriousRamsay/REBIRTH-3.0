using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace RebirthEntityStudio
{
    public static class RebirthNurseEntityConfig
    {
        public static string BuildPatch(RebirthEntityBundleReceipt receipt,string vanillaConfigPath)
        {
            if(receipt==null||receipt.assetName!="RebirthNurse")throw new ArgumentException("RebirthNurse bundle receipt required.");
            var vanilla=XDocument.Load(vanillaConfigPath);var append=new XElement("append",new XAttribute("xpath","/entity_classes"));
            string[] tiers={"Normal","Feral","Radiated","Charged","Infernal"};
            foreach(string tier in tiers)
            {
                string suffix=tier=="Normal"?"":tier,sourceName="zombieNurse"+(tier=="Infernal"?"Charged":suffix);
                XElement source=vanilla.Descendants("entity_class").Single(value=>(string)value.Attribute("name")==sourceName);
                string parent=tier=="Normal"?"zombieTemplateShort":tier=="Feral"?"RebirthNurse":tier=="Radiated"?"RebirthNurseFeral":"RebirthNurseRadiated";
                var entity=new XElement("entity_class",new XAttribute("name","RebirthNurse"+suffix),new XAttribute("extends",parent));
                foreach(var child in source.Elements())
                {
                    string property=(string)child.Attribute("name");
                    if(child.Name=="property"&&(property=="Mesh"||property=="AltMats"||property=="MatColor"||property=="Censor"||property=="PreviousTier"||property=="Tags"||(property??"").StartsWith("DismemberTag_",StringComparison.Ordinal)))continue;
                    entity.Add(new XElement(child));
                }
                string[] sourceTags=((string)source.Elements("property").Single(value=>(string)value.Attribute("name")=="Tags").Attribute("value")).Split(',');
                Property("Tags",string.Join(",",sourceTags.Where(value=>value!="allbiome"&&(tier!="Infernal"||value!="charged")).Concat(tier=="Infernal"?new[]{"rebirthNurse","infernal"}:new[]{"rebirthNurse"})));
                Property("Mesh",receipt.bundleReference);Property("PhysicsBody",receipt.physicsBodyName);
                Property("AltMats",receipt.tierMaterialReferences.Single(value=>value.Contains("_"+tier+"_Slot0")));
                Property("RebirthEntityStudioAnimatorSource","@:Entities/Zombies/Nurse/ZNurse.prefab");Property("RebirthEntityStudioHairMotion","true");
                if(!string.IsNullOrWhiteSpace(receipt.tagMapReference))Property("RebirthEntityStudioTagMap",receipt.tagMapReference);
                string template=tier=="Normal"?"ZNurse":tier=="Feral"?"ZNurseFeral":"ZNurseRadiated";
                Property("RebirthEntityStudioMaterialSource","@:Entities/Zombies/Nurse/"+template+".prefab");
                string map=tier=="Normal"?"Normal":tier=="Feral"?"Feral":"Special";
                Property("RebirthEntityStudioRmoeMap",receipt.extraTextureReferences.Single(value=>value.EndsWith("?RebirthNurse_RMOE_"+map,StringComparison.Ordinal)));
                // Independently unwrapped arms have their own PBR atlas. Bind
                // both the authored material and native packed map explicitly.
                for(int slot=1;slot<=2;slot++)
                {
                    string[] materials=receipt.tierMaterialReferences.Where(value=>value.Contains("_"+tier+"_Slot"+slot+" ")||value.EndsWith("_"+tier+"_Slot"+slot,StringComparison.Ordinal)).ToArray();
                    string[] maps=receipt.extraTextureReferences.Where(value=>value.EndsWith("?RebirthNurse_Arm"+slot+"_RMOE_"+map,StringComparison.Ordinal)).ToArray();
                    if(materials.Length==0&&maps.Length==0)continue;
                    if(materials.Length!=1||maps.Length!=1)throw new InvalidOperationException("Incomplete or ambiguous arm material/map binding for slot "+slot+" tier "+tier);
                    Property("RebirthEntityStudioMaterialSlot"+slot,materials[0]);
                    Property("RebirthEntityStudioRmoeMapSlot"+slot,maps[0]);
                }
                Property("RebirthEntityStudioTier",tier.ToLowerInvariant());
                if(tier=="Infernal"){Property("HandItem","meleeHandZombieInfernal");Property("MoveSpeedPattern","^infernalMoveSpeedPattern");}
                if(receipt.diagnosticOnly)Property("RebirthEntityStudioDiagnostic","true");
                if(tier!="Normal")Property("PreviousTier",tier=="Feral"?"RebirthNurse":tier=="Radiated"?"RebirthNurseFeral":"RebirthNurseRadiated");
                append.Add(entity);
                void Property(string name,string value){entity.Elements("property").Where(item=>(string)item.Attribute("name")==name).Remove();entity.Add(new XElement("property",new XAttribute("name",name),new XAttribute("value",value)));}
            }
            return new XDocument(new XElement("configs",append)).ToString();
        }
    }
}

