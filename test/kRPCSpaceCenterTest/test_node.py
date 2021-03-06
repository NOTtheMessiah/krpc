import unittest
import testingtools
from mathtools import vector, norm, normalize
import krpc

class TestNode(testingtools.TestCase):

    @classmethod
    def setUpClass(cls):
        testingtools.new_save()
        testingtools.set_circular_orbit('Kerbin', 100000)
        cls.conn = krpc.connect(name='TestNode')
        cls.vessel = cls.conn.space_center.active_vessel
        cls.control = cls.vessel.control
        for node in cls.control.nodes:
            node.remove()

    @classmethod
    def tearDownClass(cls):
        cls.conn.close()

    def check(self, node, deltav):
        self.assertClose(deltav[0], node.prograde)
        self.assertClose(deltav[1], node.normal)
        self.assertClose(deltav[2], node.radial)
        self.assertClose(norm(deltav), node.delta_v)
        self.assertClose(norm(deltav), node.remaining_delta_v, 0.2)

        bv = node.burn_vector(node.reference_frame)
        self.assertClose(norm(deltav), norm(bv))
        self.assertClose((0,1,0), normalize(bv))

        orbital_bv = node.burn_vector(node.orbital_reference_frame)
        self.assertClose(norm(deltav), norm(orbital_bv))
        self.assertClose((-deltav[2],deltav[0],deltav[1]), orbital_bv)

        d = node.direction(node.reference_frame)
        self.assertClose((0,1,0), d)
        orbital_d = node.direction(node.orbital_reference_frame)
        self.assertClose(normalize((-deltav[2],deltav[0],deltav[1])), orbital_d)

    def test_add_node(self):
        start_ut = self.conn.space_center.ut
        ut = start_ut + 60
        deltav = [100,200,-350]
        node = self.control.add_node(ut, *deltav)
        self.assertClose(ut, node.ut, error=1)
        self.assertClose(ut - start_ut, node.time_to, error=1)
        self.check(node, deltav)
        node.remove()

    def test_remove_node(self):
        node = self.control.add_node(self.conn.space_center.ut, 0, 0, 0)
        node.remove()
        with self.assertRaises (krpc.client.RPCError):
            node.prograde = 0

    def test_remove_nodes(self):
        node0 = self.control.add_node(self.conn.space_center.ut+15, 4, -2, 1)
        node1 = self.control.add_node(self.conn.space_center.ut+40, 1, 3, 2)
        node2 = self.control.add_node(self.conn.space_center.ut+60, 0, 4, 0)
        self.control.remove_nodes()
        # TODO: don't skip the following
        #with self.assertRaises (krpc.client.RPCError):
        #    node.prograde = 0

    def test_get_nodes(self):
        self.assertEqual([], self.control.nodes)
        node0 = self.control.add_node(self.conn.space_center.ut+35, 4, -2, 1)
        self.assertEqual([node0], self.control.nodes)
        node1 = self.control.add_node(self.conn.space_center.ut+15, 1, 3, 2)
        self.assertEqual([node1, node0], self.control.nodes)
        node2 = self.control.add_node(self.conn.space_center.ut+60, 0, 4, 0)
        self.assertEqual([node1, node0, node2], self.control.nodes)
        self.control.remove_nodes()
        self.assertEqual([], self.control.nodes)

    def test_setters(self):
        start_ut = self.conn.space_center.ut
        ut = start_ut + 60
        node = self.control.add_node(ut, 0, 0, 0)
        v = [-50,500,-150]
        ut2 = ut + 500
        node.ut = ut2
        node.prograde = v[0]
        node.normal = v[1]
        node.radial = v[2]
        self.assertClose(ut2, node.ut, error=1)
        self.assertClose(ut2 - start_ut, node.time_to, error=1)
        self.check(node, v)
        node.remove()

    def test_set_magnitude(self):
        node = self.control.add_node(self.conn.space_center.ut, 1, -2, 3)
        magnitude = 128
        node.delta_v = magnitude
        v = vector(normalize([1,-2,3])) * magnitude
        self.check(node, v)
        node.remove()

    def test_orbit(self):
        start_ut = self.conn.space_center.ut
        ut = start_ut + 60
        v = [100,0,0]
        node = self.control.add_node(ut, *v)
        self.check(node, v)

        orbit0 = self.vessel.orbit
        orbit1 = node.orbit

        # Check semi-major axis using vis-viva equation
        GM = self.conn.space_center.bodies['Kerbin'].gravitational_parameter
        vsq = (orbit0.speed + v[0])**2
        r = orbit0.radius
        self.assertClose (GM / ((2*GM/r) - vsq), orbit1.semi_major_axis, error=0.1)

        # Check there is no inclination change
        self.assertClose(orbit0.inclination, orbit1.inclination)

        # Check the eccentricity
        rp = orbit1.periapsis
        ra = orbit1.apoapsis
        e = (ra - rp) / (ra + rp)
        self.assertGreater(orbit1.eccentricity, orbit0.eccentricity)
        self.assertClose(e, orbit1.eccentricity)

if __name__ == "__main__":
    unittest.main()
