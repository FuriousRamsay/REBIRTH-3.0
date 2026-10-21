"""Authored NOT RUN: pure topology predicates only, no Bridge/network/game."""
from copy import deepcopy
import unittest
from multipeer_batch_live import qualify

class Peer:
    def __init__(self, local, mirror=None): self.local, self.mirror = local, mirror
    def call(self, endpoint, **args):
        return {"domains": {"topology": deepcopy(self.mirror if "player" in args and self.mirror else self.local)}}

class TopologyFixture(unittest.TestCase):
    def peers(self, dedicated=False):
        client = dict(schema=1, gameName="CodexTest", processIdentity="client-process", ownerKey="client-owner", creationId="client-character",
                      entityId=2, worldKey="shared-native-world", remote=True, connected=True, localActor=True, server=False, dedicated=False)
        host = dict(client, processIdentity="host-process", ownerKey="host-owner", creationId="host-character", entityId=1,
                    remote=False, connected=False, server=True, dedicated=dedicated)
        mirror = dict(client, processIdentity="host-process", remote=False, server=True, localActor=False, dedicated=dedicated, authoritativePeer=True)
        return {"host": Peer(mirror if dedicated else host, mirror), "client": Peer(client)}
    def test_independent_listen(self): qualify(self.peers(), False)
    def test_dedicated_mirror(self): qualify(self.peers(True), True)
    def test_same_process_rejected(self):
        peers=self.peers(); peers["client"].local["processIdentity"]="host-process"
        with self.assertRaises(AssertionError): qualify(peers, False)
    def test_same_identity_rejected(self):
        peers=self.peers(); peers["host"].local["ownerKey"]="client-owner"
        with self.assertRaises(AssertionError): qualify(peers, False)
    def test_fake_remote_rejected(self):
        peers=self.peers(); peers["client"].local["remote"]=False
        with self.assertRaises(AssertionError): qualify(peers, False)
    def test_wrong_world_rejected(self):
        peers=self.peers(); peers["client"].local["worldKey"]="other"
        with self.assertRaises(AssertionError): qualify(peers, False)
    def test_wrong_authoritative_mirror_rejected(self):
        peers=self.peers(); peers["host"].mirror["creationId"]="other-life"
        with self.assertRaises(AssertionError): qualify(peers, False)
    def test_real_save_rejected(self):
        peers=self.peers(); peers["client"].local["gameName"]="UserSave"
        with self.assertRaises(AssertionError): qualify(peers, False)

if __name__ == "__main__": unittest.main()
