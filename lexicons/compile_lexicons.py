import os, os.path, sys
import glob
from xml.etree import ElementTree

ElementTree.register_namespace('', 'http://www.w3.org/2005/01/pronunciation-lexicon')
ElementTree.register_namespace('xsi', 'http://www.w3.org/2001/XMLSchema-instance')

with open('lexicons/merge.pls','w') as output:

    # TODO: Change directory to a directory where all English lexicons from multiple authors are.
    directory = 'lexicons/Characters-Locations-Polly'
    pls_files = glob.glob(directory + "/*.pls")
    xml_element_tree = None

    for pls_file in pls_files:
        root = ElementTree.parse(pls_file).getroot()

        # Take in the first XML file as is
        if xml_element_tree is None:
            xml_element_tree = root

        # Append every other file to the end
        else:
            xml_element_tree.extend(root)

    # TODO: Why is this not outputting proper IPA characters for the phonemes?
    print(ElementTree.tostring(xml_element_tree).decode('utf-8'))
    output.write(ElementTree.tostring(xml_element_tree).decode('utf-8'))
